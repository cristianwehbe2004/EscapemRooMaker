import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import RoomCanvas from "./RoomCanvas";
import { RoomState } from "../../store/gameStore";

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
      interactables: [
        {
          id: "chest",
          name: "Chest",
          x: 10,
          y: 10,
          width: 50,
          height: 30,
          color: "#333",
          available: true,
        },
      ],
    };

    const onInspect = jest.fn();
    const onPickup = jest.fn();
    render(<RoomCanvas room={room} onInspect={onInspect} onPickup={onPickup} />);

    expect(screen.getByText("Test Chamber")).toBeInTheDocument();
    expect(screen.getByText("Chest")).toBeInTheDocument();

    const interactableButtons = screen.getAllByRole("button", { name: "rect" });
    const interactable = interactableButtons[1];
    fireEvent.click(interactable);
    fireEvent.dblClick(interactable);

    expect(onInspect).toHaveBeenCalledWith("chest");
    expect(onPickup).toHaveBeenCalledWith("chest");
  });
});
