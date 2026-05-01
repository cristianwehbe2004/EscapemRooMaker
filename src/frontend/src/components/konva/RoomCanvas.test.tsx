import React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import RoomCanvas from "./RoomCanvas";
import { RoomState } from "../../types/gameState";

jest.mock("react-konva", () => ({
  Stage: ({ children }: { children: React.ReactNode }) => <div data-testid="stage">{children}</div>,
  Layer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Rect: ({ onClick, onDblClick }: { onClick?: () => void; onDblClick?: () => void }) => (
    <button onClick={onClick} onDoubleClick={onDblClick}>
      rect
    </button>
  ),
  Text: ({ text }: { text: string }) => <span>{text}</span>,
}));

describe("RoomCanvas", () => {
  it("renders room title and triggers inspect/pickup actions", () => {
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
    expect(screen.getByText("Chest")).toBeInTheDocument();

    const interactableButtons = screen.getAllByRole("button", { name: "rect" });
    const interactable = interactableButtons[2];
    fireEvent.click(interactable);
    fireEvent.dblClick(interactable);

    expect(onInspect).toHaveBeenCalledWith("chest");
    expect(onPickup).toHaveBeenCalledWith("chest");
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

    const interactable = screen.getAllByRole("button", { name: "rect" })[1];
    fireEvent.click(interactable);
    fireEvent.dblClick(interactable);

    expect(onInspect).not.toHaveBeenCalled();
    expect(onPickup).not.toHaveBeenCalled();
  });
});
