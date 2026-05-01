import { SyncState } from "../store/gameStore";
import { RecoverSessionResult, SessionSnapshotEnvelope } from "../types/realtime";

const REPLAY_SETTLE_MS = 650;

const wait = (ms: number): Promise<void> =>
  new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });

type RecoveryControllerDependencies = {
  sessionId: string;
  lastKnownVersion: number;
  setSyncState: (state: SyncState) => void;
  setReplayedDiffCount: (count: number) => void;
  setConnectionError?: (message: string | null) => void;
  recoverSession: (sessionId: string, lastKnownVersion: number) => Promise<RecoverSessionResult>;
  requestSnapshot: (sessionId: string) => Promise<SessionSnapshotEnvelope>;
  applySnapshot: (snapshot: SessionSnapshotEnvelope) => void;
  onSynced?: () => void;
};

const runSnapshotRecovery = async ({
  sessionId,
  setSyncState,
  setReplayedDiffCount,
  requestSnapshot,
  applySnapshot,
  onSynced,
}: Omit<
  RecoveryControllerDependencies,
  "lastKnownVersion" | "recoverSession" | "setConnectionError"
>): Promise<void> => {
  setSyncState("recovering");
  const snapshot = await requestSnapshot(sessionId);
  applySnapshot(snapshot);
  setReplayedDiffCount(0);
  setSyncState("synced");
  onSynced?.();
};

export const runSessionRecovery = async (dependencies: RecoveryControllerDependencies): Promise<void> => {
  const {
    sessionId,
    lastKnownVersion,
    setSyncState,
    setReplayedDiffCount,
    setConnectionError,
    recoverSession,
    requestSnapshot,
    applySnapshot,
    onSynced,
  } = dependencies;

  try {
    setSyncState("recovering");
    const recoverResult = await recoverSession(sessionId, lastKnownVersion);
    setReplayedDiffCount(recoverResult.replayedDiffCount);

    if (recoverResult.snapshotSent) {
      setSyncState("synced");
      onSynced?.();
      return;
    }

    if (recoverResult.replayedDiffCount > 0) {
      setSyncState("replaying");
      await wait(REPLAY_SETTLE_MS);
      setSyncState("synced");
      onSynced?.();
      return;
    }

    await runSnapshotRecovery({
      sessionId,
      setSyncState,
      setReplayedDiffCount,
      requestSnapshot,
      applySnapshot,
      onSynced,
    });
  } catch {
    try {
      await runSnapshotRecovery({
        sessionId,
        setSyncState,
        setReplayedDiffCount,
        requestSnapshot,
        applySnapshot,
        onSynced,
      });
    } catch (error) {
      setConnectionError?.(error instanceof Error ? error.message : "Snapshot recovery failed.");
      throw error;
    }
  }
};

