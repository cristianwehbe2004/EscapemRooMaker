import React from "react";
import { act, fireEvent, render, screen } from "@testing-library/react";
import RoomCanvas from "./RoomCanvas";
import { RoomState } from "../../types/gameState";

jest.mock("react-konva", () => ({
  Stage: ({ children }: { children: React.ReactNode }) => <div data-testid="stage">{children}</div>,
  Layer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Group: ({
    children,
    id,
    x,
    y,
    rotation,
    opacity,
  }: {
    children: React.ReactNode;
    id?: string;
    x?: number;
    y?: number;
    rotation?: number;
    opacity?: number;
  }) => (
    <div
      data-testid={id}
      data-x={x}
      data-y={y}
      data-rotation={rotation}
      data-opacity={opacity}
    >
      {children}
    </div>
  ),
  Rect: ({
    id,
    x,
    y,
    width,
    height,
    rotation,
    opacity,
    onClick,
    onDblClick,
    onDblTap,
    onMouseEnter,
    onMouseLeave,
  }: {
    id?: string;
    x?: number;
    y?: number;
    width?: number;
    height?: number;
    rotation?: number;
    opacity?: number;
    onClick?: () => void;
    onDblClick?: () => void;
    onDblTap?: () => void;
    onMouseEnter?: () => void;
    onMouseLeave?: () => void;
  }) => (
    <button
      data-testid={id}
      data-x={x}
      data-y={y}
      data-width={width}
      data-height={height}
      data-rotation={rotation}
      data-opacity={opacity}
      data-clickable={Boolean(onClick)}
      onClick={onClick}
      onDoubleClick={() => {
        onDblClick?.();
        onDblTap?.();
      }}
      onMouseEnter={onMouseEnter}
      onMouseLeave={onMouseLeave}
    >
      rect
    </button>
  ),
  Circle: () => <span>circle</span>,
  Line: () => <span>line</span>,
  Ellipse: ({
    id,
    x,
    y,
    radiusX,
    radiusY,
    onClick,
    onDblClick,
    onDblTap,
  }: {
    id?: string;
    x?: number;
    y?: number;
    radiusX?: number;
    radiusY?: number;
    onClick?: () => void;
    onDblClick?: () => void;
    onDblTap?: () => void;
  }) => (
    <button
      data-testid={id}
      data-x={x}
      data-y={y}
      data-radius-x={radiusX}
      data-radius-y={radiusY}
      data-clickable={Boolean(onClick)}
      onClick={onClick}
      onDoubleClick={() => {
        onDblClick?.();
        onDblTap?.();
      }}
    >
      ellipse
    </button>
  ),
  Image: () => <span>image</span>,
  Text: ({ text }: { text: string }) => <span>{text}</span>,
}));

describe("RoomCanvas", () => {
  afterEach(() => {
    jest.useRealTimers();
  });

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

  it("mounts the clocktower note and lock onto the door hit areas", () => {
    const room: RoomState = {
      roomName: "Clocktower Foyer",
      width: 960,
      height: 620,
      backgroundColor: "#0b1220",
      assets: [],
      layers: [],
      hotspots: [
        {
          id: "final-door",
          name: "Final Door",
          x: 714,
          y: 150,
          width: 146,
          height: 300,
          color: "#7a4a2a",
          available: true,
          visible: true,
          locked: true,
          interactive: true,
          visualKind: "door",
        },
        {
          id: "door-note",
          name: "Door Note",
          x: 10,
          y: 20,
          width: 22,
          height: 18,
          color: "#fde047",
          available: true,
          visible: true,
          locked: false,
          interactive: true,
          visualKind: "note",
        },
        {
          id: "final-lock",
          name: "Final Lock",
          x: 40,
          y: 40,
          width: 20,
          height: 20,
          color: "#f4b860",
          available: true,
          visible: true,
          locked: true,
          interactive: true,
          visualKind: "lock",
          targetableModes: ["use"],
        },
      ],
      objectStates: [
        { id: "final-door", visible: true, available: true, locked: true, interactive: true },
        { id: "door-note", visible: true, available: true, locked: false, interactive: true },
        { id: "final-lock", visible: true, available: true, locked: true, interactive: true },
      ],
    };

    render(<RoomCanvas room={room} onInspect={jest.fn()} onPickup={jest.fn()} />);

    const noteHit = screen.getByTestId("hotspot-hit-door-note");
    const lockHit = screen.getByTestId("hotspot-hit-final-lock");

    expect(noteHit).toHaveAttribute("data-x", "747.58");
    expect(noteHit).toHaveAttribute("data-y", "198");
    expect(lockHit).toHaveAttribute("data-x", "789.92");
    expect(lockHit).toHaveAttribute("data-y", "297");
  });

  it("keeps the attached lock clickable in use mode and routes the lock target", async () => {
    const room: RoomState = {
      roomName: "Clocktower Foyer",
      width: 960,
      height: 620,
      backgroundColor: "#0b1220",
      assets: [],
      layers: [],
      hotspots: [
        {
          id: "final-door",
          name: "Final Door",
          x: 714,
          y: 150,
          width: 146,
          height: 300,
          color: "#7a4a2a",
          available: true,
          visible: true,
          locked: true,
          interactive: true,
          visualKind: "door",
        },
        {
          id: "final-lock",
          name: "Final Lock",
          x: 790,
          y: 296,
          width: 56,
          height: 76,
          color: "#f4b860",
          available: true,
          visible: true,
          locked: true,
          interactive: true,
          visualKind: "lock",
          targetableModes: ["use"],
          targetableItemIds: ["brass-key"],
        },
      ],
      objectStates: [
        { id: "final-door", visible: true, available: true, locked: true, interactive: true },
        { id: "final-lock", visible: true, available: true, locked: true, interactive: true },
      ],
    };

    const onInspect = jest.fn();
    render(
      <RoomCanvas
        room={room}
        onInspect={onInspect}
        onPickup={jest.fn()}
        interactionMode="use"
        selectedInventoryItemId="brass-key"
        selectedInventoryItem={{
          id: "brass-key",
          label: "Brass Key",
          quantity: 1,
          type: "key",
          stack: false,
          status: "ready",
          usableTargetIds: ["final-lock"],
        }}
      />
    );

    fireEvent.click(screen.getByTestId("hotspot-hit-final-lock"));
    await new Promise((resolve) => window.setTimeout(resolve, 220));

    expect(onInspect).toHaveBeenCalledWith("final-lock");
  });

  it("plays the attached lock opening animation before removing it", () => {
    jest.useFakeTimers();

    const room: RoomState = {
      roomName: "Clocktower Foyer",
      width: 960,
      height: 620,
      backgroundColor: "#0b1220",
      assets: [],
      layers: [],
      hotspots: [
        {
          id: "final-door",
          name: "Final Door",
          x: 714,
          y: 150,
          width: 146,
          height: 300,
          color: "#7a4a2a",
          available: true,
          visible: true,
          locked: true,
          interactive: true,
          visualKind: "door",
        },
        {
          id: "final-lock",
          name: "Final Lock",
          x: 790,
          y: 296,
          width: 56,
          height: 76,
          color: "#f4b860",
          available: true,
          visible: true,
          locked: true,
          interactive: true,
          visualKind: "lock",
        },
      ],
      objectStates: [
        { id: "final-door", visible: true, available: true, locked: true, interactive: true },
        { id: "final-lock", visible: true, available: true, locked: true, interactive: true },
      ],
    };

    const { rerender } = render(<RoomCanvas room={room} onInspect={jest.fn()} onPickup={jest.fn()} />);

    rerender(
      <RoomCanvas
        room={{
          ...room,
          hotspots: [
            room.hotspots[0],
            {
              ...room.hotspots[1],
              locked: false,
              visible: false,
              available: false,
              interactive: false,
            },
          ],
          objectStates: [
            room.objectStates[0],
            { id: "final-lock", visible: false, available: false, locked: false, interactive: false },
          ],
        }}
        onInspect={jest.fn()}
        onPickup={jest.fn()}
      />
    );

    expect(screen.getByTestId("hotspot-animation-final-lock")).toBeInTheDocument();

    act(() => {
      jest.advanceTimersByTime(360);
    });
    const animationNode = screen.getByTestId("hotspot-animation-final-lock");
    expect(Number(animationNode.getAttribute("data-rotation"))).toBeGreaterThan(0);
    expect(Number(animationNode.getAttribute("data-opacity"))).toBeLessThan(1);

    act(() => {
      jest.advanceTimersByTime(400);
    });
    expect(screen.queryByTestId("hotspot-animation-final-lock")).not.toBeInTheDocument();
    expect(screen.queryByTestId("hotspot-hit-final-lock")).not.toBeInTheDocument();
  });
});
