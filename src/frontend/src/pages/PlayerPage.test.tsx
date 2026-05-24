import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { clearAuthSession } from "../auth/authSession";
import PlayerPage from "./PlayerPage";
import { useGameStore } from "../store/gameStore";

const mockStart = jest.fn();
const mockStop = jest.fn();
const mockSubmitAction = jest.fn();
const mockRecoverSession = jest.fn();
const mockRequestSnapshot = jest.fn();

const featuredRoomsResponse = {
  items: [
    {
      roomId: "room-easy",
      name: "Clocktower Foyer",
      description: "Easy room",
      createdAtUtc: new Date().toISOString(),
      ratingCount: 2,
      averageRating: 4.5,
      viewerRating: null,
      isFeatured: true,
      difficulty: "easy",
      estimatedMinutes: 3,
    },
    {
      roomId: "room-medium",
      name: "Crypt of Echoes",
      description: "Medium room",
      createdAtUtc: new Date().toISOString(),
      ratingCount: 3,
      averageRating: 4.2,
      viewerRating: null,
      isFeatured: true,
      difficulty: "medium",
      estimatedMinutes: 5,
    },
    {
      roomId: "room-hard",
      name: "Velvet Vault",
      description: "Hard room",
      createdAtUtc: new Date().toISOString(),
      ratingCount: 5,
      averageRating: 4.8,
      viewerRating: null,
      isFeatured: true,
      difficulty: "hard",
      estimatedMinutes: 7,
    },
  ],
  page: 1,
  pageSize: 12,
  total: 3,
};

const buildSessionSummary = (overrides?: Partial<Record<string, unknown>>) => ({
  sessionId: "session-123",
  roomId: "room-easy",
  roomName: "Clocktower Foyer",
  status: "Pending",
  durationMinutes: 3,
  startedAtUtc: new Date().toISOString(),
  endedAtUtc: null,
  endsAtUtc: new Date(Date.now() + 3 * 60 * 1000).toISOString(),
  serverTimeUtc: new Date().toISOString(),
  remainingSeconds: 180,
  isQuickPlay: false,
  playerJoinPath: "/player?sessionId=session-123",
  gmJoinPath: "/gm?sessionId=session-123",
  actorId: "guest-123",
  displayName: "Player",
  joinMode: "player",
  canSubmitActions: true,
  ...overrides,
});

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
    clearAuthSession();
    window.localStorage.clear();

    jest.spyOn(globalThis, "fetch").mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === "string" ? input : input.toString();
      if (url.endsWith("/api/auth/login") && init?.method === "POST") {
        return {
          ok: true,
          json: async () => ({
            accessToken: "token-123",
            refreshToken: "refresh-123",
            accessTokenExpiresAtUtc: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
            user: {
              id: "user-1",
              username: "player1",
              email: "player1@escaperoom.local",
              role: "Player",
            },
          }),
          headers: new Headers({ "content-type": "application/json" }),
        } as Response;
      }

      if (url.includes("/api/library/rooms")) {
        return {
          ok: true,
          json: async () => featuredRoomsResponse,
          headers: new Headers({ "content-type": "application/json" }),
        } as Response;
      }

      if (url.endsWith("/api/player/sessions") && init?.method === "POST") {
        return {
          ok: true,
          json: async () => buildSessionSummary(),
          headers: new Headers({ "content-type": "application/json" }),
        } as Response;
      }

      if (url.includes("/join") && init?.method === "POST") {
        return {
          ok: true,
          json: async () => buildSessionSummary({ status: "Active", joinMode: "player", canSubmitActions: true }),
          headers: new Headers({ "content-type": "application/json" }),
        } as Response;
      }

      if (url.includes("/quick-start") && init?.method === "POST") {
        return {
          ok: true,
          json: async () => buildSessionSummary({ status: "Active", isQuickPlay: true }),
          headers: new Headers({ "content-type": "application/json" }),
        } as Response;
      }

      return {
        ok: true,
        json: async () => buildSessionSummary(),
        headers: new Headers({ "content-type": "application/json" }),
      } as Response;
    });

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

  it("renders a richer landing hero with create/join controls", () => {
    render(<PlayerPage />);

    expect(screen.getByText("EscapeRoom Live")).toBeInTheDocument();
    expect(screen.getByText("Create Session")).toBeInTheDocument();
    expect(screen.getByText("Join Existing Session")).toBeInTheDocument();
  });

  it("opens map menu and creates session from selected featured room", async () => {
    render(<PlayerPage />);

    fireEvent.click(screen.getByText("Create Session"));

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.stringContaining("/api/library/rooms?featured=true"),
        expect.anything()
      );
    });

    expect(await screen.findByText("Clocktower Foyer")).toBeInTheDocument();
    expect(screen.getByText("Crypt of Echoes")).toBeInTheDocument();
    expect(screen.getByText("Velvet Vault")).toBeInTheDocument();
    expect(screen.getByText("Estimated 5 min")).toBeInTheDocument();
    expect(screen.getByText("Estimated 7 min")).toBeInTheDocument();

    const createButtons = screen.getAllByText("Create Lobby");
    fireEvent.click(createButtons[0]);

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        "http://localhost:5130/api/player/sessions",
        expect.objectContaining({ method: "POST", body: expect.stringContaining("room-easy") })
      );
      expect(mockStart).toHaveBeenCalledWith("session-123", undefined, expect.any(Object));
    });

    expect(screen.getByText(/Share this link:/i)).toBeInTheDocument();
  });

  it("joins an already active session as a shared player", async () => {
    render(<PlayerPage />);

    fireEvent.change(screen.getByPlaceholderText("Session UUID"), {
      target: { value: "session-123" },
    });
    fireEvent.click(screen.getByText("Join Session"));

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        "http://localhost:5130/api/player/sessions/session-123/join",
        expect.objectContaining({ method: "POST" })
      );
    });

    expect(screen.queryByText(/Spectator mode is active/i)).not.toBeInTheDocument();
    expect(screen.getByText(/role player/i)).toBeInTheDocument();
  });

  it("signs in through the UI and reuses the bearer token for session join", async () => {
    render(<PlayerPage />);

    fireEvent.change(screen.getByPlaceholderText("Email"), { target: { value: "player1@escaperoom.local" } });
    fireEvent.change(screen.getByPlaceholderText("Password"), { target: { value: "Player123!" } });
    fireEvent.click(screen.getAllByRole("button", { name: "Sign In" })[1]);

    await waitFor(() => {
      expect(screen.getByText(/signed in as player1/i)).toBeInTheDocument();
    });

    fireEvent.change(screen.getByPlaceholderText("Session UUID"), {
      target: { value: "session-123" },
    });
    fireEvent.click(screen.getByText("Join Session"));

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        "http://localhost:5130/api/player/sessions/session-123/join",
        expect.objectContaining({
          method: "POST",
          headers: expect.objectContaining({
            Authorization: "Bearer token-123",
          }),
        })
      );
    });
  });

  it("keeps the player in a recoverable state when realtime join fails", async () => {
    mockStart.mockRejectedValueOnce(new Error("Failed to invoke 'JoinSession' due to an error on the server."));

    render(<PlayerPage />);

    fireEvent.click(screen.getByText("Create Session"));

    expect(await screen.findByText("Clocktower Foyer")).toBeInTheDocument();
    fireEvent.click(screen.getAllByText("Quick Start")[0]);

    await waitFor(() => {
      expect(mockStart).toHaveBeenCalledWith("session-123", undefined, expect.any(Object));
    });

    expect(await screen.findByText(/Failed to invoke 'JoinSession'/i)).toBeInTheDocument();
    expect(screen.getByText("Retry Connect")).toBeInTheDocument();
    expect(screen.queryByText("Inspect Desk Note")).not.toBeInTheDocument();
  });

  it("shows contextual fallback actions and maps drawer Open to inspect", async () => {
    useGameStore.setState((current) => ({
      ...current,
      state: {
        ...current.state,
        room: {
          ...current.state.room,
          hotspots: [
            {
              id: "door-note",
              name: "Door Note",
              visualKind: "note",
              x: 10,
              y: 10,
              width: 50,
              height: 50,
              color: "#fff7c2",
              visible: true,
              available: true,
              locked: false,
              interactive: true,
            },
            {
              id: "left-drawer",
              name: "Workbench Drawer",
              visualKind: "drawer",
              x: 70,
              y: 20,
              width: 60,
              height: 40,
              color: "#7a5035",
              visible: true,
              available: true,
              locked: false,
              interactive: true,
            },
            {
              id: "final-lock",
              name: "Final Lock",
              visualKind: "lock",
              x: 140,
              y: 40,
              width: 40,
              height: 60,
              color: "#f4b860",
              visible: true,
              available: true,
              locked: false,
              interactive: true,
              targetableModes: ["use"],
              targetableItemIds: ["brass-key"],
            },
          ],
          objectStates: [
            { id: "door-note", visible: true, available: true, locked: false, interactive: true },
            { id: "left-drawer", visible: true, available: true, locked: false, interactive: true },
            { id: "final-lock", visible: true, available: true, locked: false, interactive: true },
          ],
        },
        inventory: [
          {
            id: "brass-key",
            label: "Brass Key",
            quantity: 1,
            type: "key",
            stack: false,
            status: "ready",
            usableTargetIds: ["final-lock"],
          },
        ],
      },
    }));

    render(<PlayerPage />);
    fireEvent.click(screen.getByText("Create Session"));
    expect(await screen.findByText("Clocktower Foyer")).toBeInTheDocument();
    fireEvent.click(screen.getAllByText("Quick Start")[0]);

    await waitFor(() => {
      expect(mockStart).toHaveBeenCalled();
    });

    expect(screen.getByRole("button", { name: "Open" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pickup" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Open" }));
    await waitFor(() => {
      expect(mockSubmitAction).toHaveBeenCalledWith(
        "session-123",
        expect.objectContaining({ actionType: "inspect", target: "left-drawer" })
      );
    });
  });

  it("enables Use action for lock targets only when use mode and a valid item are selected", async () => {
    useGameStore.setState((current) => ({
      ...current,
      state: {
        ...current.state,
        room: {
          ...current.state.room,
          hotspots: [
            {
              id: "final-lock",
              name: "Final Lock",
              visualKind: "lock",
              x: 140,
              y: 40,
              width: 40,
              height: 60,
              color: "#f4b860",
              visible: true,
              available: true,
              locked: false,
              interactive: true,
              targetableModes: ["use"],
              targetableItemIds: ["brass-key"],
            },
          ],
          objectStates: [{ id: "final-lock", visible: true, available: true, locked: false, interactive: true }],
        },
        inventory: [
          {
            id: "brass-key",
            label: "Brass Key",
            quantity: 1,
            type: "key",
            stack: false,
            status: "ready",
            usableTargetIds: ["final-lock"],
          },
        ],
      },
    }));

    render(<PlayerPage />);
    fireEvent.click(screen.getByText("Create Session"));
    expect(await screen.findByText("Clocktower Foyer")).toBeInTheDocument();
    fireEvent.click(screen.getAllByText("Quick Start")[0]);

    await waitFor(() => {
      expect(mockStart).toHaveBeenCalled();
    });

    const useButtonBeforeMode = screen.getAllByRole("button", { name: "Use" }).find((button) => button.hasAttribute("disabled"));
    expect(useButtonBeforeMode).toBeDefined();

    fireEvent.click(screen.getByRole("button", { name: /Brass Key/i }));
    const useButtonsAfterSelect = screen.getAllByRole("button", { name: "Use" });
    const inventoryUseButton = useButtonsAfterSelect.find((button) => !button.hasAttribute("disabled"));
    expect(inventoryUseButton).toBeDefined();
    fireEvent.click(inventoryUseButton!);

    const enabledUseButtons = screen.getAllByRole("button", { name: "Use" }).filter((button) => !button.hasAttribute("disabled"));
    fireEvent.click(enabledUseButtons[0]);

    await waitFor(() => {
      expect(mockSubmitAction).toHaveBeenCalledWith(
        "session-123",
        expect.objectContaining({ actionType: "inventory.use", target: "final-lock" })
      );
    });
  });

  it("allows inventory use when usableTargetIds matches hotspot objectId", async () => {
    useGameStore.setState((current) => ({
      ...current,
      state: {
        ...current.state,
        room: {
          ...current.state.room,
          hotspots: [
            {
              id: "final-lock-hotspot",
              objectId: "final-lock",
              name: "Final Lock",
              visualKind: "lock",
              x: 140,
              y: 40,
              width: 40,
              height: 60,
              color: "#f4b860",
              visible: true,
              available: true,
              locked: false,
              interactive: true,
              targetableModes: ["use"],
            },
          ],
          objectStates: [{ id: "final-lock", visible: true, available: true, locked: false, interactive: true }],
        },
        inventory: [
          {
            id: "brass-key",
            label: "Brass Key",
            quantity: 1,
            type: "key",
            stack: false,
            status: "ready",
            usableTargetIds: ["final-lock"],
          },
        ],
      },
    }));

    render(<PlayerPage />);
    fireEvent.click(screen.getByText("Create Session"));
    expect(await screen.findByText("Clocktower Foyer")).toBeInTheDocument();
    fireEvent.click(screen.getAllByText("Quick Start")[0]);

    await waitFor(() => {
      expect(mockStart).toHaveBeenCalled();
    });

    fireEvent.click(screen.getByRole("button", { name: /Brass Key/i }));
    const useButtonsAfterSelect = screen.getAllByRole("button", { name: "Use" });
    const inventoryUseButton = useButtonsAfterSelect.find((button) => !button.hasAttribute("disabled"));
    expect(inventoryUseButton).toBeDefined();
    fireEvent.click(inventoryUseButton!);

    const enabledUseButtons = screen.getAllByRole("button", { name: "Use" }).filter((button) => !button.hasAttribute("disabled"));
    fireEvent.click(enabledUseButtons[0]);

    await waitFor(() => {
      expect(mockSubmitAction).toHaveBeenCalledWith(
        "session-123",
        expect.objectContaining({ actionType: "inventory.use", target: "final-lock" })
      );
    });
  });

  it("shows Use for reader targets and submits the object as inventory.use", async () => {
    useGameStore.setState((current) => ({
      ...current,
      state: {
        ...current.state,
        room: {
          ...current.state.room,
          hotspots: [
            {
              id: "final-reader-hotspot",
              objectId: "final-reader",
              name: "Exit Reader",
              visualKind: "switch",
              variant: "reader",
              x: 140,
              y: 40,
              width: 40,
              height: 60,
              color: "#f4b860",
              visible: true,
              available: true,
              locked: false,
              interactive: true,
              targetableModes: ["use"],
              targetableItemIds: ["exit-keycard"],
            },
          ],
          objectStates: [{ id: "final-reader", visible: true, available: true, locked: false, interactive: true }],
        },
        inventory: [
          {
            id: "exit-keycard",
            label: "Ivory Keycard",
            quantity: 1,
            type: "keycard",
            stack: false,
            status: "ready",
            usableTargetIds: ["final-reader"],
          },
        ],
      },
    }));

    render(<PlayerPage />);
    fireEvent.click(screen.getByText("Create Session"));
    expect(await screen.findByText("Clocktower Foyer")).toBeInTheDocument();
    fireEvent.click(screen.getAllByText("Quick Start")[0]);

    await waitFor(() => {
      expect(mockStart).toHaveBeenCalled();
    });

    fireEvent.click(screen.getByRole("button", { name: /Ivory Keycard/i }));
    const inventoryUseButton = screen.getAllByRole("button", { name: "Use" }).find((button) => !button.hasAttribute("disabled"));
    expect(inventoryUseButton).toBeDefined();
    fireEvent.click(inventoryUseButton!);

    const enabledUseButtons = screen.getAllByRole("button", { name: "Use" }).filter((button) => !button.hasAttribute("disabled"));
    expect(enabledUseButtons.length).toBeGreaterThan(0);
    fireEvent.click(enabledUseButtons[0]);

    await waitFor(() => {
      expect(mockSubmitAction).toHaveBeenCalledWith(
        "session-123",
        expect.objectContaining({ actionType: "inventory.use", target: "final-reader" })
      );
    });
  });
});
