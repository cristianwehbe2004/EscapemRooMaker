import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import RoomCanvas from "../components/konva/RoomCanvas";
import ActionFeedbackPanel from "../components/ui/ActionFeedbackPanel";
import InventoryPanel, { InventoryInteractionMode } from "../components/ui/InventoryPanel";
import ReconnectBanner from "../components/ui/ReconnectBanner";
import { useActionCooldown } from "../hooks/useActionCooldown";
import { GameRealtimeClient } from "../realtime/gameRealtimeClient";
import { diffNeedsSnapshotResync, useGameStore } from "../store/gameStore";
import { ActionError, parseActionError } from "../types/actionError";
import { PlayerActionEnvelope } from "../types/realtime";

const apiBaseUrl = process.env.REACT_APP_API_BASE_URL ?? "http://localhost:5000";

const PlayerPage: React.FC = () => {
  const [sessionInput, setSessionInput] = useState("");
  const [actorInput, setActorInput] = useState("player-local");
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const [pendingActionLabel, setPendingActionLabel] = useState<string | null>(null);
  const [lastActionLabel, setLastActionLabel] = useState<string | null>(null);
  const [actionError, setActionError] = useState<ActionError | null>(null);
  const [selectedInventoryItemId, setSelectedInventoryItemId] = useState<string | null>(null);
  const [inventoryInteractionMode, setInventoryInteractionMode] = useState<InventoryInteractionMode>("none");
  const [pendingResyncToken, setPendingResyncToken] = useState(0);
  const [replayedDiffCount, setReplayedDiffCount] = useState(0);
  const [showSyncedBanner, setShowSyncedBanner] = useState(false);
  const [trackedCooldownKeys, setTrackedCooldownKeys] = useState<Record<string, { label: string }>>({});
  const [cooldownTick, setCooldownTick] = useState(0);
  const { runWithCooldown, getRemainingMs } = useActionCooldown(900);
  const lastSnapshotSyncAtRef = useRef(0);
  const clientRef = useRef<GameRealtimeClient | null>(null);

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
    window.setTimeout(() => {
      setShowSyncedBanner(false);
    }, 2200);
  }, []);

  const runSnapshotRecovery = useCallback(
    async (client: GameRealtimeClient, activeSessionId: string) => {
      setSyncState("recovering");
      const snapshot = await client.requestSnapshot(activeSessionId);
      applySnapshot(snapshot);
      setReplayedDiffCount(0);
      setSyncState("synced");
      showSyncedToast();
    },
    [applySnapshot, setSyncState, showSyncedToast]
  );

  const client = useMemo(
    () =>
      new GameRealtimeClient(
        { baseUrl: apiBaseUrl },
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
              setSyncState("recovering");
              const recoverResult = await runtimeClient.recoverSession(activeSessionId, lastKnownVersion);
              setReplayedDiffCount(recoverResult.replayedDiffCount);

              if (recoverResult.replayedDiffCount > 0) {
                setSyncState("replaying");
                window.setTimeout(() => {
                  setSyncState("synced");
                  showSyncedToast();
                }, 650);
              } else {
                await runSnapshotRecovery(runtimeClient, activeSessionId);
              }
            } catch {
              await runSnapshotRecovery(runtimeClient, activeSessionId);
            }
          },
          onDisconnected: () => setConnectionStatus({ connected: false }),
        }
      ),
    [applyDiff, applySnapshot, lastKnownVersion, runSnapshotRecovery, setConnectionStatus, setSyncState, showSyncedToast]
  );

  useEffect(() => {
    clientRef.current = client;
  }, [client]);

  useEffect(() => {
    const intervalId = window.setInterval(() => {
      setCooldownTick((current) => current + 1);
    }, 250);

    return () => window.clearInterval(intervalId);
  }, []);

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
    return () => {
      if (typeof client.stop === "function") {
        void client.stop();
      }
    };
  }, [client]);

  const connect = async () => {
    if (!sessionInput) {
      setConnectionError("Please provide a session id.");
      return;
    }

    try {
      setConnectionError(null);
      setActionError(null);
      const ack = await client.start(sessionInput);
      setSessionId(ack.sessionId);
      setConnectionStatus({ connected: true });
      setSyncState("synced");
      setLastActionLabel(`Joined session ${ack.sessionId}`);
    } catch (error) {
      setConnectionStatus({ connected: false });
      setConnectionError(error instanceof Error ? error.message : "Could not connect to game hub.");
    }
  };

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
      setConnectionError("Connect to a session first.");
      return;
    }

    const actionLabel = `${actionType} -> ${targetId}`;
    const cooldownKey = options?.cooldownKey ?? `${actionType}:${targetId}`;
    const action: PlayerActionEnvelope = {
      actionType,
      actor: actorInput || "player-local",
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
      setActionError(parseActionError(error, cooldownKey));
      setConnectionError(error instanceof Error ? error.message : "Failed to submit action.");
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

    await submitAction(
      "inventory.combine",
      itemId,
      {
        primaryItemId: selectedInventoryItemId,
        secondaryItemId: itemId,
      },
      {
        cooldownKey: `inventory.combine:${selectedInventoryItemId}:${itemId}`,
        afterSuccess: () => {
          clearInventoryIntent();
        },
      }
    );
  };

  const handleInspect = async (targetId: string) => {
    if (inventoryInteractionMode === "use" && selectedInventoryItemId) {
      await submitAction(
        "inventory.use",
        targetId,
        {
          itemId: selectedInventoryItemId,
        },
        {
          cooldownKey: `inventory.use:${selectedInventoryItemId}:${targetId}`,
          afterSuccess: () => {
            clearInventoryIntent();
          },
        }
      );
      return;
    }

    await submitAction("inspect", targetId);
  };

  return (
    <main className="mx-auto flex max-w-6xl flex-col gap-4 p-4 text-slate-100">
      <h1 className="text-2xl font-semibold">Player Experience (Day 4 Slice)</h1>
      <ReconnectBanner syncState={syncState} replayedDiffCount={replayedDiffCount} showSynced={showSyncedBanner} />
      <div className="flex flex-wrap items-center gap-3 rounded bg-slate-900 p-3">
        <input
          value={sessionInput}
          onChange={(event) => setSessionInput(event.target.value)}
          placeholder="Session UUID"
          className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
        />
        <input
          value={actorInput}
          onChange={(event) => setActorInput(event.target.value)}
          placeholder="Actor"
          className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
        />
        <button onClick={connect} className="rounded bg-blue-600 px-4 py-2 text-white">
          Join Session
        </button>
        <span className="text-sm">
          Status: {connected ? "connected" : "disconnected"} | Sync: {syncState} | Version: {sessionVersion}
        </span>
      </div>

      {connectionError && <p className="rounded border border-red-700 bg-red-950 p-2 text-red-200">{connectionError}</p>}

      <section className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
        <RoomCanvas
          room={state.room}
          onInspect={(targetId) => {
            void handleInspect(targetId);
          }}
          onPickup={(targetId) => submitAction("pickup", targetId)}
          selectedInventoryItemId={selectedInventoryItemId}
          interactionMode={inventoryInteractionMode}
        />
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

              setInventoryInteractionMode(mode);
            }}
            onClearSelection={clearInventoryIntent}
            disabled={!connected}
          />
          <ActionFeedbackPanel
            lastActionLabel={lastActionLabel}
            pendingActionLabel={pendingActionLabel}
            actionError={actionError}
            cooldownChips={cooldownChips}
            messages={state.messages}
          />
        </div>
      </section>
    </main>
  );
};

export default PlayerPage;
