import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import PlayerPage from "./PlayerPage";
import { useGameStore } from "../store/gameStore";

const mockStart = jest.fn();
const mockStop = jest.fn();
const mockSubmitAction = jest.fn();
const mockRecoverSession = jest.fn();
const mockRequestSnapshot = jest.fn();

const sessionSummary = {
  sessionId: "session-123",
  roomId: "room-123",
  roomName: "Vault Puzzle",
  status: "Active",
  durationMinutes: 60,
  startedAtUtc: new Date().toISOString(),
  endedAtUtc: null,
  endsAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  serverTimeUtc: new Date().toISOString(),
  remainingSeconds: 3600,
  isQuickPlay: false,
  playerJoinPath: "/player?sessionId=session-123",
  gmJoinPath: "/gm?sessionId=session-123",
  actorId: "guest-123",
  displayName: "Player",
};

jest.mock("../realtime/gameRealtimeClient", () => ({
  GameRealtimeClient: function MockGameRealtimeClient(
    _options: unknown,
    _handlers: unknown
  ) {
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
    jest.spyOn(globalThis, "fetch").mockResolvedValue({
      ok: true,
      json: async () => sessionSummary,
    } as Response);
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
    (globalThis.fetch as jest.Mock | undefined)?.mockRestore?.();
  });

  it("shows the player entry flow before connecting", () => {
    render(<PlayerPage />);

    expect(screen.getByText("Escape Room")).toBeInTheDocument();
    expect(screen.getByText("Start")).toBeInTheDocument();
    expect(screen.getByText("Create New Session")).toBeInTheDocument();
    expect(screen.getAllByText("Join Session")).toHaveLength(2);
  });

  it("quick starts a timed session and shows action feedback after acting", async () => {
    render(<PlayerPage />);

    fireEvent.click(screen.getByText("Start"));

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        "http://localhost:5000/api/player/sessions/quick-start",
        expect.objectContaining({ method: "POST" })
      );
      expect(mockStart).toHaveBeenCalledWith(
        "session-123",
        undefined,
        expect.objectContaining({ displayName: "Player" })
      );
    });

    expect(screen.getByText("Inventory")).toBeInTheDocument();
    expect(screen.getByText("Flashlight")).toBeInTheDocument();
    expect(screen.getByText("Action Feedback")).toBeInTheDocument();

    fireEvent.click(screen.getByText("Inspect Desk Note"));
    await waitFor(() => {
      expect(mockSubmitAction).toHaveBeenCalledTimes(1);
      expect(screen.getByText(/last action:/i)).toHaveTextContent("inspect -> desk-note");
    });
  });

  it("joins an existing active session", async () => {
    render(<PlayerPage />);

    fireEvent.change(screen.getByPlaceholderText("Session UUID"), {
      target: { value: "session-123" },
    });
    fireEvent.click(screen.getAllByText("Join Session")[1]);

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        "http://localhost:5000/api/player/sessions/session-123/join",
        expect.objectContaining({ method: "POST" })
      );
      expect(mockStart).toHaveBeenCalledWith(
        "session-123",
        undefined,
        expect.objectContaining({ guestActorId: expect.stringMatching(/^guest-/) })
      );
    });
  });

  it("shows reconnect banner when reconnecting", () => {
    useGameStore.setState({ syncState: "reconnecting" });
    render(<PlayerPage />);

    expect(screen.getByText(/reconnecting to the session/i)).toBeInTheDocument();
  });

  it("routes structured server rate-limit errors to action feedback only", async () => {
    mockSubmitAction.mockRejectedValue(
      new Error(
        JSON.stringify({
          code: "rate_limited",
          message: "Action rate limited.",
          retryAfterMs: 1200,
          policyName: "player-action-default",
        })
      )
    );

    render(<PlayerPage />);

    fireEvent.change(screen.getByPlaceholderText("Session UUID"), {
      target: { value: "session-123" },
    });
    fireEvent.click(screen.getAllByText("Join Session")[1]);

    await waitFor(() => {
      expect(mockStart).toHaveBeenCalled();
    });

    fireEvent.click(screen.getByText("Inspect Desk Note"));

    await waitFor(() => {
      expect(screen.getByText("Action rate limited.")).toBeInTheDocument();
      expect(screen.getByText(/source: server rate limit/i)).toBeInTheDocument();
      expect(screen.queryByText(/code\":\"rate_limited/i)).not.toBeInTheDocument();
    });
  });

});
