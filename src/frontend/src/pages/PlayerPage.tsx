import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import RoomCanvas from "../components/konva/RoomCanvas";
import ActionFeedbackPanel from "../components/ui/ActionFeedbackPanel";
import InventoryPanel, { InventoryInteractionMode } from "../components/ui/InventoryPanel";
import ReconnectBanner from "../components/ui/ReconnectBanner";
import { useActionCooldown } from "../hooks/useActionCooldown";
import { GameRealtimeClient } from "../realtime/gameRealtimeClient";
import { runSessionRecovery } from "../realtime/recoveryController";
import { diffNeedsSnapshotResync, useGameStore } from "../store/gameStore";
import { ActionError, parseActionError } from "../types/actionError";
import { LibraryRoomListItemDto, LibraryRoomsResponse } from "../types/library";
import { CreateSessionRequest, JoinSessionRequest, PlayerSessionSummary } from "../types/playerSession";
import { InventoryCombineActionPayload, InventoryUseActionPayload, PlayerActionEnvelope } from "../types/realtime";

const apiBaseUrl = process.env.REACT_APP_API_BASE_URL ?? "http://localhost:5000";
const guestActorStorageKey = "escape-room.guestActorId";
const fixedSessionMinutes = 10;

type PlayerPhase = "home" | "map" | "lobby" | "game";

const ensureGuestActorId = (): string => {
  if (typeof window === "undefined") {
    return `guest-${Math.random().toString(16).slice(2)}`;
  }

  const existing = window.localStorage.getItem(guestActorStorageKey);
  if (existing) {
    return existing;
  }

  const generated = `guest-${crypto.randomUUID()}`;
  window.localStorage.setItem(guestActorStorageKey, generated);
  return generated;
};

const formatSeconds = (seconds: number): string => {
  const safeSeconds = Math.max(0, Math.floor(seconds));
  const minutes = Math.floor(safeSeconds / 60);
  const remainingSeconds = safeSeconds % 60;
  return `${minutes}:${remainingSeconds.toString().padStart(2, "0")}`;
};

const PlayerPage: React.FC = () => {
  const sessionIdFromUrl = typeof window !== "undefined" ? new URLSearchParams(window.location.search).get("sessionId") : null;
  const [phase, setPhase] = useState<PlayerPhase>(sessionIdFromUrl ? "lobby" : "home");
  const [displayName, setDisplayName] = useState("Player");
  const [accessToken, setAccessToken] = useState("");
  const [sessionInput, setSessionInput] = useState(sessionIdFromUrl ?? "");
  const [guestActorId] = useState(ensureGuestActorId);
  const [playerSession, setPlayerSession] = useState<PlayerSessionSummary | null>(null);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const [pendingActionLabel, setPendingActionLabel] = useState<string | null>(null);
  const [lastActionLabel, setLastActionLabel] = useState<string | null>(null);
  const [actionError, setActionError] = useState<ActionError | null>(null);
  const [selectedInventoryItemId, setSelectedInventoryItemId] = useState<string | null>(null);
  const [inventoryInteractionMode, setInventoryInteractionMode] = useState<InventoryInteractionMode>("none");
  const [focusedTargetId, setFocusedTargetId] = useState<string | null>(null);
  const [pendingResyncToken, setPendingResyncToken] = useState(0);
  const [replayedDiffCount, setReplayedDiffCount] = useState(0);
  const [showSyncedBanner, setShowSyncedBanner] = useState(false);
  const [trackedCooldownKeys, setTrackedCooldownKeys] = useState<Record<string, { label: string }>>({});
  const [cooldownTick, setCooldownTick] = useState(0);
  const [timerTick, setTimerTick] = useState(0);
  const [featuredRooms, setFeaturedRooms] = useState<LibraryRoomListItemDto[]>([]);
  const [featuredLoading, setFeaturedLoading] = useState(false);
  const [selectedRoomId, setSelectedRoomId] = useState<string | null>(null);
  const { runWithCooldown, getRemainingMs } = useActionCooldown(900);
  const lastSnapshotSyncAtRef = useRef(0);
  const clientRef = useRef<GameRealtimeClient | null>(null);
  const autoJoinAttemptedRef = useRef(false);

  const {
    sessionId,
    connected,
    syncState,
    sessionVersion,
    lastKnownVersion,
    state,
    applyDiff,
    applySnapshot,
    setConnectionStatus,
    setSyncState,
    setSessionId,
  } = useGameStore();

  const clearInventoryIntent = useCallback(() => {
    setSelectedInventoryItemId(null);
    setInventoryInteractionMode("none");
  }, []);

  const showSyncedToast = useCallback(() => {
    setShowSyncedBanner(true);
    window.setTimeout(() => setShowSyncedBanner(false), 2200);
  }, []);

  const client = useMemo(
    () =>
      new GameRealtimeClient(
        {
          baseUrl: apiBaseUrl,
          getAccessToken: () => accessToken,
        },
        {
          onDiff: (diff) => {
            applyDiff(diff);
            if (diffNeedsSnapshotResync(diff)) {
              setPendingResyncToken((current) => current + 1);
            }
          },
          onSnapshot: (snapshot) => {
            applySnapshot(snapshot);
            setSyncState("synced");
          },
          onReconnecting: () => {
            setSyncState("reconnecting");
            setConnectionStatus({ connected: false });
          },
          onReconnected: async (activeSessionId) => {
            setConnectionStatus({ connected: true });
            if (!activeSessionId) {
              setSyncState("synced");
              return;
            }

            const runtimeClient = clientRef.current;
            if (!runtimeClient) {
              setConnectionError("Realtime client was not ready for recovery.");
              return;
            }

            try {
              await runSessionRecovery({
                sessionId: activeSessionId,
                lastKnownVersion,
                setSyncState,
                setReplayedDiffCount,
                setConnectionError,
                recoverSession: runtimeClient.recoverSession.bind(runtimeClient),
                requestSnapshot: runtimeClient.requestSnapshot.bind(runtimeClient),
                applySnapshot,
                onSynced: showSyncedToast,
              });
            } catch {
              // Recovery controller sets a connection error when needed.
            }
          },
          onDisconnected: () => setConnectionStatus({ connected: false }),
        }
      ),
    [accessToken, applyDiff, applySnapshot, lastKnownVersion, setConnectionStatus, setSyncState, showSyncedToast]
  );

  useEffect(() => {
    clientRef.current = client;
  }, [client]);

  useEffect(() => {
    const intervalId = window.setInterval(() => {
      setCooldownTick((current) => current + 1);
      setTimerTick((current) => current + 1);
    }, 250);

    return () => window.clearInterval(intervalId);
  }, []);

  useEffect(() => {
    return () => {
      if (typeof client.stop === "function") {
        void client.stop();
      }
    };
  }, [client]);

  const resolveHttpError = async (response: Response): Promise<string> => {
    const contentType = response.headers.get("content-type") ?? "";
    if (contentType.includes("application/json")) {
      try {
        const body = (await response.json()) as { detail?: string; title?: string; message?: string };
        return body.detail ?? body.message ?? body.title ?? `Request failed: ${response.status}`;
      } catch {
        return `Request failed: ${response.status}`;
      }
    }

    const text = await response.text();
    return text || `Request failed: ${response.status}`;
  };

  const callSessionApi = async <T,>(path: string, method = "GET", body?: unknown): Promise<T> => {
    const response = await fetch(`${apiBaseUrl}${path}`, {
      method,
      headers: {
        "Content-Type": "application/json",
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    });

    if (!response.ok) {
      throw new Error(await resolveHttpError(response));
    }

    return (await response.json()) as T;
  };

  const connectRealtime = async (summary: PlayerSessionSummary) => {
    const ack = await client.start(summary.sessionId, undefined, {
      displayName: summary.displayName || displayName,
      guestActorId: summary.actorId || guestActorId,
    });
    setSessionId(ack.sessionId);
    setConnectionStatus({ connected: true });
    setSyncState("synced");
    setLastActionLabel(`Joined session ${ack.sessionId}`);
  };

  const createSessionBody = (roomId: string): CreateSessionRequest => ({
    roomId,
    displayName,
  });

  const joinRequestBody = (): JoinSessionRequest => ({
    displayName,
    guestActorId,
  });

  const loadFeaturedRooms = async () => {
    setFeaturedLoading(true);
    try {
      const response = await callSessionApi<LibraryRoomsResponse>("/api/library/rooms?featured=true&sort=name&page=1&pageSize=12");
      const rooms = response.items ?? [];
      setFeaturedRooms(rooms);
      if (!selectedRoomId && rooms.length > 0) {
        setSelectedRoomId(rooms[0].roomId);
      }
    } catch (error) {
      setConnectionError(error instanceof Error ? error.message : "Could not load featured rooms.");
    } finally {
      setFeaturedLoading(false);
    }
  };

  const openMapMenu = async () => {
    setConnectionError(null);
    setPhase("map");
    if (featuredRooms.length === 0) {
      await loadFeaturedRooms();
    }
  };

  const quickStartWithRoom = async (roomId: string) => {
    setConnectionError(null);
    setActionError(null);
    try {
      const summary = await callSessionApi<PlayerSessionSummary>("/api/player/sessions/quick-start", "POST", createSessionBody(roomId));
      setPlayerSession(summary);
      setSessionInput(summary.sessionId);
      setPhase("game");
      await connectRealtime(summary);
    } catch (error) {
      setConnectionError(error instanceof Error ? error.message : "Could not quick start a session.");
    }
  };

  const createHostedSessionWithRoom = async (roomId: string) => {
    setConnectionError(null);
    setActionError(null);
    try {
      const summary = await callSessionApi<PlayerSessionSummary>("/api/player/sessions", "POST", createSessionBody(roomId));
      setPlayerSession(summary);
      setSessionInput(summary.sessionId);
      setPhase("lobby");
      await connectRealtime(summary);
    } catch (error) {
      setConnectionError(error instanceof Error ? error.message : "Could not create a session.");
    }
  };

  const joinHostedSession = async (sessionIdToJoin = sessionInput.trim()) => {
    if (!sessionIdToJoin) {
      setConnectionError("Provide a session id.");
      return;
    }

    setConnectionError(null);
    setActionError(null);
    try {
      const summary = await callSessionApi<PlayerSessionSummary>(
        `/api/player/sessions/${sessionIdToJoin}/join`,
        "POST",
        joinRequestBody()
      );
      setPlayerSession(summary);
      setSessionInput(summary.sessionId);
      setPhase(summary.status === "Active" ? "game" : "lobby");
      await connectRealtime(summary);
    } catch (error) {
      setConnectionError(error instanceof Error ? error.message : "Could not join the session.");
    }
  };

  const startHostedSession = async () => {
    const activeSessionId = playerSession?.sessionId ?? sessionInput.trim();
    if (!activeSessionId) {
      setConnectionError("Create or join a session first.");
      return;
    }

    setConnectionError(null);
    setActionError(null);
    try {
      const summary = await callSessionApi<PlayerSessionSummary>(
        `/api/player/sessions/${activeSessionId}/start`,
        "POST",
        joinRequestBody()
      );
      setPlayerSession(summary);
      setPhase("game");
      if (!connected) {
        await connectRealtime(summary);
      } else {
        const snapshot = await client.requestSnapshot(summary.sessionId);
        applySnapshot(snapshot);
      }
    } catch (error) {
      setConnectionError(error instanceof Error ? error.message : "Could not start the session.");
    }
  };

  useEffect(() => {
    if (!sessionIdFromUrl || autoJoinAttemptedRef.current) {
      return;
    }

    autoJoinAttemptedRef.current = true;
    setSessionInput(sessionIdFromUrl);
    void joinHostedSession(sessionIdFromUrl);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionIdFromUrl]);

  const cooldownChips = useMemo(() => {
    void cooldownTick;

    return Object.entries(trackedCooldownKeys)
      .map(([key, meta]) => ({
        key,
        label: meta.label,
        remainingMs: getRemainingMs(key),
      }))
      .filter((entry) => entry.remainingMs > 0)
      .sort((a, b) => b.remainingMs - a.remainingMs)
      .slice(0, 4);
  }, [cooldownTick, getRemainingMs, trackedCooldownKeys]);

  const selectedInventoryItem = useMemo(
    () => state.inventory.find((item) => item.id === selectedInventoryItemId) ?? null,
    [selectedInventoryItemId, state.inventory]
  );

  const focusedHotspot = useMemo(
    () => state.room.hotspots.find((hotspot) => hotspot.id === focusedTargetId) ?? null,
    [focusedTargetId, state.room.hotspots]
  );

  useEffect(() => {
    setTrackedCooldownKeys((current) => {
      const activeEntries = Object.entries(current).filter(([key]) => getRemainingMs(key) > 0);
      if (activeEntries.length === Object.keys(current).length) {
        return current;
      }

      return Object.fromEntries(activeEntries);
    });
  }, [cooldownTick, getRemainingMs]);

  useEffect(() => {
    if (pendingResyncToken === 0 || !sessionId || !connected) {
      return;
    }

    const now = Date.now();
    if (now - lastSnapshotSyncAtRef.current < 1200) {
      return;
    }

    lastSnapshotSyncAtRef.current = now;
    void client
      .requestSnapshot(sessionId)
      .then((snapshot) => {
        applySnapshot(snapshot);
        setSyncState("synced");
      })
      .catch((error) => {
        setConnectionError(error instanceof Error ? error.message : "Snapshot recovery failed.");
        setActionError(parseActionError(error));
      });
  }, [applySnapshot, client, connected, pendingResyncToken, sessionId, setSyncState]);

  useEffect(() => {
    if (!selectedInventoryItemId) {
      return;
    }

    if (!state.inventory.some((item) => item.id === selectedInventoryItemId)) {
      clearInventoryIntent();
    }
  }, [clearInventoryIntent, selectedInventoryItemId, state.inventory]);

  const canSubmitActions = (playerSession?.canSubmitActions ?? state.session?.canSubmitActions ?? true) && playerSession?.joinMode !== "spectator";

  const submitAction = async (
    actionType: string,
    targetId: string,
    payload: Record<string, unknown> = {},
    options?: {
      cooldownKey?: string;
      afterSuccess?: () => void;
    }
  ) => {
    if (!sessionId) {
      setConnectionError("Join a session first.");
      return;
    }

    if (!canSubmitActions) {
      setActionError({
        source: "network",
        message: "You are spectating this active session. Action submission is disabled.",
      });
      return;
    }

    const actor = playerSession?.actorId || guestActorId;
    const actionLabel = `${actionType} -> ${targetId}`;
    const cooldownKey = options?.cooldownKey ?? `${actionType}:${targetId}`;
    const action: PlayerActionEnvelope = {
      actionType,
      actor,
      target: targetId,
      payload,
      clientActionId: crypto.randomUUID(),
      timestampUtc: new Date().toISOString(),
    };

    try {
      setConnectionError(null);
      setActionError(null);
      setTrackedCooldownKeys((current) => ({
        ...current,
        [cooldownKey]: { label: actionType },
      }));

      const gate = await runWithCooldown(cooldownKey, async () => {
        setPendingActionLabel(actionLabel);
        await client.submitAction(sessionId, action);
        setLastActionLabel(actionLabel);
        options?.afterSuccess?.();
      });

      if (!gate.allowed) {
        setActionError({
          source: "local-cooldown",
          message: `Action cooling down. Try again in ${gate.remainingMs}ms.`,
          retryAfterMs: gate.remainingMs,
          actionKey: cooldownKey,
        });
      }
    } catch (error) {
      const parsedError = parseActionError(error, cooldownKey);
      setActionError(parsedError);
      if (parsedError.source === "network") {
        setConnectionError(parsedError.message);
      }
    } finally {
      setPendingActionLabel(null);
    }
  };

  const handleInventoryItemClick = async (itemId: string) => {
    if (inventoryInteractionMode !== "combine") {
      setSelectedInventoryItemId(itemId);
      return;
    }

    if (!selectedInventoryItemId || selectedInventoryItemId === itemId) {
      setSelectedInventoryItemId(itemId);
      return;
    }

    const selectedItem = selectedInventoryItem;
    if (selectedItem?.status !== "ready") {
      setActionError({
        source: "local-cooldown",
        message: `${selectedItem?.label ?? "Selected item"} is not usable right now.`,
      });
      return;
    }

    if (
      selectedItem?.combinableWithIds &&
      selectedItem.combinableWithIds.length > 0 &&
      !selectedItem.combinableWithIds.includes(itemId)
    ) {
      setActionError({
        source: "local-cooldown",
        message: `${selectedItem.label} cannot be combined with that item.`,
      });
      return;
    }

    const payload: InventoryCombineActionPayload = {
      primaryItemId: selectedInventoryItemId,
      secondaryItemId: itemId,
    };

    await submitAction("inventory.combine", itemId, payload, {
      cooldownKey: `inventory.combine:${selectedInventoryItemId}:${itemId}`,
      afterSuccess: clearInventoryIntent,
    });
  };

  const handleInspect = async (targetId: string) => {
    setFocusedTargetId(targetId);
    if (inventoryInteractionMode === "use" && selectedInventoryItemId) {
      const selectedItem = selectedInventoryItem;
      if (selectedItem?.status !== "ready") {
        setActionError({
          source: "local-cooldown",
          message: `${selectedItem?.label ?? "Selected item"} is not usable right now.`,
        });
        return;
      }

      if (
        selectedItem?.usableTargetIds &&
        selectedItem.usableTargetIds.length > 0 &&
        !selectedItem.usableTargetIds.includes(targetId)
      ) {
        setActionError({
          source: "local-cooldown",
          message: `${selectedItem.label} cannot be used on that target.`,
        });
        return;
      }

      const payload: InventoryUseActionPayload = {
        itemId: selectedInventoryItemId,
      };

      await submitAction("inventory.use", targetId, payload, {
        cooldownKey: `inventory.use:${selectedInventoryItemId}:${targetId}`,
        afterSuccess: clearInventoryIntent,
      });
      return;
    }

    await submitAction("inspect", targetId);
  };

  const sessionState = state.session;
  const status = sessionState?.status ?? playerSession?.status ?? "Not Started";
  const endsAtUtc = sessionState?.endsAtUtc ?? playerSession?.endsAtUtc;
  const remainingSeconds = useMemo(() => {
    void timerTick;
    if (status !== "Active") {
      return sessionState?.remainingSeconds ?? playerSession?.remainingSeconds ?? fixedSessionMinutes * 60;
    }

    if (!endsAtUtc) {
      return sessionState?.remainingSeconds ?? playerSession?.remainingSeconds ?? 0;
    }

    return Math.max(0, Math.ceil((new Date(endsAtUtc).getTime() - Date.now()) / 1000));
  }, [endsAtUtc, playerSession?.remainingSeconds, sessionState?.remainingSeconds, status, timerTick]);
  const isGameOver = status === "Completed" || status === "Expired" || status === "Cancelled";

  const renderHero = () => (
    <section className="grid gap-5 rounded-3xl border border-sky-500/30 bg-gradient-to-br from-slate-950 via-slate-900 to-sky-950 p-6 shadow-[0_0_40px_rgba(14,165,233,0.18)] lg:grid-cols-[1.2fr_0.8fr]">
      <div>
        <p className="text-xs uppercase tracking-[0.22em] text-sky-300">EscapeRoom Live</p>
        <h1 className="mt-3 text-4xl font-semibold leading-tight text-slate-50">Choose a room. Beat it in 10 minutes.</h1>
        <p className="mt-3 max-w-xl text-sm text-slate-300">
          Create a solo or hosted session from two curated maps. Inventory interactions, lock logic, and realtime spectating are fully server-authoritative.
        </p>
        <div className="mt-6 flex flex-wrap gap-3">
          <button onClick={() => void openMapMenu()} className="rounded-xl bg-sky-500 px-5 py-3 text-sm font-semibold text-slate-950">
            Create Session
          </button>
          <a href="/library" className="rounded-xl border border-slate-500 px-5 py-3 text-sm text-slate-200">
            Browse Full Library
          </a>
        </div>
      </div>
      <div className="rounded-2xl border border-slate-700 bg-slate-900/80 p-4">
        <h2 className="text-sm font-semibold uppercase tracking-wider text-slate-300">Join Existing Session</h2>
        <div className="mt-3 grid gap-2">
          <input
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
            placeholder="Display name"
            className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
          />
          <input
            value={accessToken}
            onChange={(event) => setAccessToken(event.target.value)}
            placeholder="Optional bearer token"
            className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
          />
          <input
            value={sessionInput}
            onChange={(event) => setSessionInput(event.target.value)}
            placeholder="Session UUID"
            className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
          />
          <button onClick={() => void joinHostedSession()} className="rounded bg-indigo-600 px-4 py-2 text-white">
            Join Session
          </button>
        </div>
      </div>
    </section>
  );

  const renderMapMenu = () => (
    <section className="rounded-3xl border border-slate-700 bg-slate-900/90 p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold">Choose Your Map</h2>
          <p className="text-sm text-slate-300">All player sessions are fixed to {fixedSessionMinutes} minutes.</p>
        </div>
        <button onClick={() => void loadFeaturedRooms()} className="rounded border border-slate-500 px-3 py-2 text-sm text-slate-200">
          Refresh Rooms
        </button>
      </div>

      {featuredLoading && <p className="mt-4 text-sm text-slate-300">Loading featured rooms...</p>}

      <div className="mt-4 grid gap-4 md:grid-cols-2">
        {featuredRooms.map((room) => {
          const selected = selectedRoomId === room.roomId;
          const difficulty = (room.difficulty ?? "unknown").toString().toLowerCase();
          const chipClass =
            difficulty === "hard"
              ? "bg-rose-900/70 text-rose-200"
              : difficulty === "easy"
                ? "bg-emerald-900/70 text-emerald-200"
                : "bg-slate-700 text-slate-200";

          return (
            <article
              key={room.roomId}
              className={`rounded-2xl border p-4 transition ${selected ? "border-sky-400 bg-slate-800" : "border-slate-700 bg-slate-900"}`}
            >
              <div className="flex items-center justify-between gap-2">
                <h3 className="text-lg font-semibold">{room.name}</h3>
                <span className={`rounded-full px-2 py-1 text-xs uppercase tracking-wider ${chipClass}`}>{difficulty}</span>
              </div>
              <p className="mt-2 text-sm text-slate-300">{room.description || "No description."}</p>
              <p className="mt-2 text-xs text-slate-400">Estimated {room.estimatedMinutes ?? fixedSessionMinutes} min</p>
              <div className="mt-4 flex flex-wrap gap-2">
                <button
                  onClick={() => {
                    setSelectedRoomId(room.roomId);
                    void createHostedSessionWithRoom(room.roomId);
                  }}
                  className="rounded bg-sky-600 px-3 py-2 text-sm font-medium text-white"
                >
                  Create Lobby
                </button>
                <button
                  onClick={() => {
                    setSelectedRoomId(room.roomId);
                    void quickStartWithRoom(room.roomId);
                  }}
                  className="rounded bg-emerald-600 px-3 py-2 text-sm font-medium text-white"
                >
                  Quick Start
                </button>
              </div>
            </article>
          );
        })}
      </div>
    </section>
  );

  return (
    <main className="mx-auto flex max-w-7xl flex-col gap-4 p-4 text-slate-100">
      <ReconnectBanner syncState={syncState} replayedDiffCount={replayedDiffCount} showSynced={showSyncedBanner} />

      {phase === "home" && renderHero()}
      {phase === "map" && renderMapMenu()}

      {playerSession && phase !== "home" && (
        <section className="rounded border border-slate-700 bg-slate-900 p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h1 className="text-2xl font-semibold">{sessionState?.roomName ?? playerSession.roomName}</h1>
              <p className="text-sm text-slate-300">
                Session {playerSession.sessionId} | {connected ? "connected" : "disconnected"} | {status} | role {playerSession.joinMode}
              </p>
            </div>
            <div className="flex items-center gap-3">
              <div className={`rounded px-3 py-2 text-xl font-semibold ${remainingSeconds <= 120 ? "bg-red-950 text-red-200" : "bg-slate-800"}`}>
                {formatSeconds(remainingSeconds)}
              </div>
              {phase === "lobby" && playerSession.canSubmitActions && (
                <button onClick={startHostedSession} className="rounded bg-emerald-600 px-4 py-2 text-white">
                  Start Session
                </button>
              )}
            </div>
          </div>
          {phase === "lobby" && (
            <div className="mt-3 rounded border border-slate-700 bg-slate-950 p-3 text-sm text-slate-300">
              Share this link: {playerSession.playerJoinPath}
            </div>
          )}
        </section>
      )}

      {!canSubmitActions && playerSession?.status === "Active" && (
        <p className="rounded border border-amber-600 bg-amber-950 p-3 text-amber-200">
          Spectator mode is active for this session. You will receive live updates but cannot submit actions.
        </p>
      )}

      {connectionError && <p className="rounded border border-red-700 bg-red-950 p-2 text-red-200">{connectionError}</p>}

      {phase === "game" && (
        <section className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div className="flex flex-col gap-3">
            {isGameOver && (
              <div className="rounded border border-emerald-700 bg-emerald-950 p-3 text-emerald-100">
                {status === "Completed" ? "Room complete." : "Session ended."}
              </div>
            )}
            <RoomCanvas
              room={state.room}
              onInspect={(targetId) => {
                void handleInspect(targetId);
              }}
              onPickup={(targetId) => submitAction("pickup", targetId)}
              onHotspotFocus={setFocusedTargetId}
              selectedInventoryItemId={selectedInventoryItemId}
              selectedInventoryItem={selectedInventoryItem}
              interactionMode={inventoryInteractionMode}
            />
            <div className="flex flex-wrap items-center gap-2 rounded border border-slate-700 bg-slate-900 p-3">
              <span className="text-sm text-slate-300">Focused: {focusedHotspot?.name ?? "None"}</span>
              <button
                disabled={!focusedHotspot || isGameOver || !canSubmitActions}
                onClick={() => focusedHotspot && void submitAction("inspect", focusedHotspot.id)}
                className="rounded bg-slate-700 px-3 py-2 text-sm text-white disabled:cursor-not-allowed disabled:opacity-40"
              >
                Inspect
              </button>
              <button
                disabled={!focusedHotspot || isGameOver || !canSubmitActions}
                onClick={() => focusedHotspot && void submitAction("pickup", focusedHotspot.id)}
                className="rounded bg-amber-700 px-3 py-2 text-sm text-white disabled:cursor-not-allowed disabled:opacity-40"
              >
                Pickup
              </button>
            </div>
          </div>

          <div className="flex flex-col gap-4">
            <InventoryPanel
              items={state.inventory}
              selectedItemId={selectedInventoryItemId}
              interactionMode={inventoryInteractionMode}
              onItemClick={(itemId) => {
                void handleInventoryItemClick(itemId);
              }}
              onSetInteractionMode={(mode) => {
                if (!selectedInventoryItemId && mode !== "none") {
                  setActionError({
                    source: "local-cooldown",
                    message: "Select an inventory item first.",
                  });
                  return;
                }

                if (selectedInventoryItemId && mode !== "none") {
                  const selectedItem = state.inventory.find((item) => item.id === selectedInventoryItemId);
                  if (selectedItem?.status !== "ready") {
                    setActionError({
                      source: "local-cooldown",
                      message: `${selectedItem?.label ?? "Selected item"} is not usable right now.`,
                    });
                    return;
                  }
                }

                setInventoryInteractionMode(mode);
              }}
              onClearSelection={clearInventoryIntent}
              disabled={!connected || isGameOver || !canSubmitActions}
            />

            <aside className="rounded border border-slate-700 bg-slate-900 p-4">
              <h2 className="text-lg font-semibold">Clues</h2>
              {state.clues && state.clues.length > 0 ? (
                <ul className="mt-3 space-y-2 text-sm text-slate-200">
                  {state.clues.map((clue, index) => (
                    <li key={`${clue}-${index}`} className="rounded bg-slate-800 px-3 py-2">
                      {clue}
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="mt-3 text-sm text-slate-400">No clues discovered yet.</p>
              )}
            </aside>

            <ActionFeedbackPanel
              lastActionLabel={lastActionLabel}
              pendingActionLabel={pendingActionLabel}
              actionError={actionError}
              cooldownChips={cooldownChips}
              messages={state.messages}
            />

            <details className="rounded border border-slate-700 bg-slate-900 p-3 text-xs text-slate-400">
              <summary>Debug</summary>
              <p>Sync: {syncState}</p>
              <p>Version: {sessionVersion}</p>
              <p>Role: {playerSession?.joinMode ?? "player"}</p>
              <p>Can submit actions: {canSubmitActions ? "yes" : "no"}</p>
            </details>
          </div>
        </section>
      )}
    </main>
  );
};

export default PlayerPage;
