import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import PlayerPage from "./PlayerPage";
import { useGameStore } from "../store/gameStore";

const mockStart = jest.fn();
const mockStop = jest.fn();
const mockSubmitAction = jest.fn();
const mockRecoverSession = jest.fn();
const mockRequestSnapshot = jest.fn();

jest.mock("../realtime/gameRealtimeClient", () => ({
  GameRealtimeClient: function MockGameRealtimeClient() {
    return {
      start: mockStart,
      stop: mockStop,
      submitAction: mockSubmitAction,
      recoverSession: mockRecoverSession,
      requestSnapshot: mockRequestSnapshot,
    };
  },
}));

jest.mock("../components/konva/RoomCanvas", () => {
  return function MockRoomCanvas({
    onInspect,
    onPickup,
  }: {
    onInspect: (targetId: string) => void;
    onPickup: (targetId: string) => void;
  }) {
    return (
      <div>
        <button onClick={() => onInspect("desk-note")}>Inspect Desk Note</button>
        <button onClick={() => onPickup("rusty-key")}>Pickup Rusty Key</button>
      </div>
    );
  };
});

describe("PlayerPage", () => {
  beforeEach(() => {
    useGameStore.getState().reset();
    jest.clearAllMocks();
    mockStart.mockResolvedValue({
      sessionId: "session-123",
      replayedDiffCount: 0,
      currentVersion: 1,
      lastKnownVersion: null,
    });
    mockSubmitAction.mockResolvedValue({
      sessionVersion: 1,
      diffSequence: 1,
      correlationId: "corr-1",
      emittedAtUtc: new Date().toISOString(),
      changedEntities: [],
      emittedMessages: ["Action processed"],
      appliedEffects: [],
    });
    mockRecoverSession.mockResolvedValue({
      sessionId: "session-123",
      replayedDiffCount: 0,
      snapshotSent: true,
      currentVersion: 1,
    });
    mockRequestSnapshot.mockResolvedValue({
      sessionId: "session-123",
      sessionVersion: 1,
      stateJson: JSON.stringify(useGameStore.getState().state),
      serverTimeUtc: new Date().toISOString(),
    });
    if (!globalThis.crypto) {
      Object.defineProperty(globalThis, "crypto", {
        value: { randomUUID: () => "123e4567-e89b-12d3-a456-426614174000" },
        configurable: true,
      });
    } else {
      jest.spyOn(globalThis.crypto, "randomUUID").mockReturnValue("123e4567-e89b-12d3-a456-426614174000");
    }
  });

  afterEach(() => {
    const randomUuidMock = globalThis.crypto?.randomUUID as jest.Mock | undefined;
    if (randomUuidMock?.mockRestore) {
      randomUuidMock.mockRestore();
    }
  });

  it("renders overlays and shows action feedback after joining and acting", async () => {
    render(<PlayerPage />);

    expect(screen.getByText("Inventory")).toBeInTheDocument();
    expect(screen.getByText("Flashlight")).toBeInTheDocument();
    expect(screen.getByText("Action Feedback")).toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText("Session UUID"), {
      target: { value: "session-123" },
    });
    fireEvent.click(screen.getByText("Join Session"));

    await waitFor(() => {
      expect(mockStart).toHaveBeenCalledWith("session-123");
    });

    fireEvent.click(screen.getByText("Inspect Desk Note"));
    await waitFor(() => {
      expect(mockSubmitAction).toHaveBeenCalledTimes(1);
      expect(screen.getByText(/last action:/i)).toHaveTextContent("inspect -> desk-note");
    });
  });

  it("shows reconnect banner when reconnecting", () => {
    useGameStore.setState({ syncState: "reconnecting" });
    render(<PlayerPage />);

    expect(screen.getByText(/reconnecting to the session/i)).toBeInTheDocument();
  });
});
