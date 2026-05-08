import React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import RoomCanvas from "./RoomCanvas";
import { RoomState } from "../../types/gameState";

jest.mock("react-konva", () => ({
  Stage: ({ children }: { children: React.ReactNode }) => <div data-testid="stage">{children}</div>,
  Layer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Group: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Rect: ({
    onClick,
    onDblClick,
    onDblTap,
    onMouseEnter,
    onMouseLeave,
  }: {
    onClick?: () => void;
    onDblClick?: () => void;
    onDblTap?: () => void;
    onMouseEnter?: () => void;
    onMouseLeave?: () => void;
  }) => (
    <button onClick={onClick} onDoubleClick={() => { onDblClick?.(); onDblTap?.(); }} onMouseEnter={onMouseEnter} onMouseLeave={onMouseLeave}>
      rect
    </button>
  ),
  Circle: () => <span>circle</span>,
  Line: () => <span>line</span>,
  Ellipse: ({ onClick, onDblClick, onDblTap }: { onClick?: () => void; onDblClick?: () => void; onDblTap?: () => void }) => (
    <button onClick={onClick} onDoubleClick={() => { onDblClick?.(); onDblTap?.(); }}>
      ellipse
    </button>
  ),
  Image: () => <span>image</span>,
  Text: ({ text }: { text: string }) => <span>{text}</span>,
}));

describe("RoomCanvas", () => {
  it("renders room title and triggers inspect/pickup actions", async () => {
    const room: RoomState = {
      roomName: "Test Chamber",
      width: 500,
      height: 300,
      backgroundColor: "#0b1220",
      assets: [{ id: "bg", kind: "background", x: 0, y: 0, width: 500, height: 300, zIndex: 0, visible: true, opacity: 1 }],
      layers: [],
      hotspots: [
        {
          id: "chest",
          name: "Chest",
          x: 10,
          y: 10,
          width: 50,
          height: 30,
          color: "#333",
          available: true,
          visible: true,
          locked: false,
          interactive: true,
        },
      ],
      objectStates: [{ id: "chest", visible: true, available: true, locked: false, interactive: true }],
    };

    const onInspect = jest.fn();
    const onPickup = jest.fn();
    render(<RoomCanvas room={room} onInspect={onInspect} onPickup={onPickup} selectedInventoryItemId="inv-key" interactionMode="use" />);

    expect(screen.getByText("Test Chamber")).toBeInTheDocument();
    const interactableButtons = screen.getAllByRole("button", { name: "rect" });
    const hotspotButton = interactableButtons[interactableButtons.length - 2];
    fireEvent.click(hotspotButton);
    await new Promise((resolve) => window.setTimeout(resolve, 220));
    fireEvent.dblClick(hotspotButton);

    expect(onInspect).toHaveBeenCalledWith("chest");
    expect(onPickup).toHaveBeenCalledWith("chest");
  });

  it("shows hover preview label without auto-triggering actions", () => {
    const room: RoomState = {
      roomName: "Hover Room",
      width: 500,
      height: 300,
      backgroundColor: "#0b1220",
      assets: [],
      layers: [],
      hotspots: [
        {
          id: "desk-note",
          name: "Desk Note",
          x: 10,
          y: 10,
          width: 50,
          height: 30,
          color: "#333",
          available: true,
          visible: true,
          locked: false,
          interactive: true,
          visualKind: "note",
        },
      ],
      objectStates: [{ id: "desk-note", visible: true, available: true, locked: false, interactive: true }],
    };

    const onInspect = jest.fn();
    const onPickup = jest.fn();
    render(<RoomCanvas room={room} onInspect={onInspect} onPickup={onPickup} />);

    const buttons = screen.getAllByRole("button", { name: "rect" });
    buttons.forEach((button) => fireEvent.mouseEnter(button));

    expect(screen.getByText("Desk Note • Inspect")).toBeInTheDocument();
    expect(onInspect).not.toHaveBeenCalled();
    expect(onPickup).not.toHaveBeenCalled();
  });

  it("does not trigger actions for locked hotspot", () => {
    const room: RoomState = {
      roomName: "Locks",
      width: 400,
      height: 250,
      backgroundColor: "#000",
      assets: [],
      layers: [],
      hotspots: [
        {
          id: "vault",
          name: "Vault",
          x: 20,
          y: 20,
          width: 80,
          height: 60,
          color: "#999",
          available: true,
          visible: true,
          locked: true,
          interactive: true,
        },
      ],
      objectStates: [{ id: "vault", visible: true, available: true, locked: true, interactive: true }],
    };

    const onInspect = jest.fn();
    const onPickup = jest.fn();
    render(<RoomCanvas room={room} onInspect={onInspect} onPickup={onPickup} />);

    const interactableButtons = screen.getAllByRole("button", { name: "rect" });
    interactableButtons.forEach((button) => {
      fireEvent.click(button);
      fireEvent.dblClick(button);
    });

    expect(onInspect).not.toHaveBeenCalled();
    expect(onPickup).not.toHaveBeenCalled();
  });

  it("does not render hidden hotspots as actionable elements", () => {
    const room: RoomState = {
      roomName: "Hidden Key Room",
      width: 400,
      height: 250,
      backgroundColor: "#000",
      assets: [],
      layers: [],
      hotspots: [
        {
          id: "hidden-key",
          name: "Hidden Key",
          x: 20,
          y: 20,
          width: 80,
          height: 30,
          color: "#facc15",
          available: false,
          visible: false,
          locked: false,
          interactive: false,
          visualKind: "key",
        },
      ],
      objectStates: [{ id: "hidden-key", visible: false, available: false, locked: false, interactive: false }],
    };

    render(<RoomCanvas room={room} onInspect={jest.fn()} onPickup={jest.fn()} />);

    expect(screen.queryByText("Hidden Key • Inspect • Pick up • Use")).not.toBeInTheDocument();
  });

  it("disables interactions when the room is completed", () => {
    const room: RoomState = {
      roomName: "Completed Chamber",
      width: 500,
      height: 300,
      backgroundColor: "#0b1220",
      assets: [],
      layers: [],
      hotspots: [
        {
          id: "final-door",
          name: "Final Door",
          x: 10,
          y: 10,
          width: 50,
          height: 80,
          color: "#7a4a2a",
          available: true,
          visible: true,
          locked: false,
          interactive: true,
          visualKind: "door",
        },
      ],
      objectStates: [{ id: "final-door", visible: true, available: true, locked: false, interactive: true }],
    };

    const onInspect = jest.fn();
    const onPickup = jest.fn();
    render(<RoomCanvas room={room} onInspect={onInspect} onPickup={onPickup} disabled />);

    const interactableButtons = screen.getAllByRole("button", { name: "rect" });
    interactableButtons.forEach((button) => {
      fireEvent.click(button);
      fireEvent.dblClick(button);
    });

    expect(onInspect).not.toHaveBeenCalled();
    expect(onPickup).not.toHaveBeenCalled();
    expect(screen.getByText("The room is no longer interactive.")).toBeInTheDocument();
  });
});
