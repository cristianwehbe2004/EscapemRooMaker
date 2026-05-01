import { runSessionRecovery } from "./recoveryController";

describe("runSessionRecovery", () => {
  const buildDependencies = () => {
    const setSyncState = jest.fn();
    const setReplayedDiffCount = jest.fn();
    const setConnectionError = jest.fn();
    const applySnapshot = jest.fn();
    const onSynced = jest.fn();
    const recoverSession = jest.fn();
    const requestSnapshot = jest.fn();

    return {
      sessionId: "session-123",
      lastKnownVersion: 5,
      setSyncState,
      setReplayedDiffCount,
      setConnectionError,
      recoverSession,
      requestSnapshot,
      applySnapshot,
      onSynced,
    };
  };

  it("runs replay path when recover returns missed diffs", async () => {
    const deps = buildDependencies();
    deps.recoverSession.mockResolvedValue({
      sessionId: "session-123",
      replayedDiffCount: 3,
      snapshotSent: false,
      currentVersion: 8,
    });

    await runSessionRecovery(deps);

    expect(deps.setSyncState).toHaveBeenNthCalledWith(1, "recovering");
    expect(deps.setSyncState).toHaveBeenNthCalledWith(2, "replaying");
    expect(deps.setSyncState).toHaveBeenLastCalledWith("synced");
    expect(deps.requestSnapshot).not.toHaveBeenCalled();
    expect(deps.onSynced).toHaveBeenCalledTimes(1);
  });

  it("honors explicit snapshotSent response from recover", async () => {
    const deps = buildDependencies();
    deps.recoverSession.mockResolvedValue({
      sessionId: "session-123",
      replayedDiffCount: 0,
      snapshotSent: true,
      currentVersion: 6,
    });

    await runSessionRecovery(deps);

    expect(deps.requestSnapshot).not.toHaveBeenCalled();
    expect(deps.setSyncState).toHaveBeenNthCalledWith(1, "recovering");
    expect(deps.setSyncState).toHaveBeenLastCalledWith("synced");
    expect(deps.onSynced).toHaveBeenCalledTimes(1);
  });

  it("falls back to snapshot path when recover has no replay and no snapshot sent", async () => {
    const deps = buildDependencies();
    deps.recoverSession.mockResolvedValue({
      sessionId: "session-123",
      replayedDiffCount: 0,
      snapshotSent: false,
      currentVersion: 5,
    });
    deps.requestSnapshot.mockResolvedValue({
      sessionId: "session-123",
      sessionVersion: 6,
      stateJson: JSON.stringify({ inventory: [{ id: "inv-key", label: "Key", quantity: 1 }] }),
      serverTimeUtc: new Date().toISOString(),
    });

    await runSessionRecovery(deps);

    expect(deps.requestSnapshot).toHaveBeenCalledWith("session-123");
    expect(deps.applySnapshot).toHaveBeenCalledTimes(1);
    expect(deps.setReplayedDiffCount).toHaveBeenLastCalledWith(0);
    expect(deps.setSyncState).toHaveBeenLastCalledWith("synced");
  });

  it("uses snapshot fallback when recover throws", async () => {
    const deps = buildDependencies();
    deps.recoverSession.mockRejectedValue(new Error("recover failed"));
    deps.requestSnapshot.mockResolvedValue({
      sessionId: "session-123",
      sessionVersion: 6,
      stateJson: JSON.stringify({ inventory: [{ id: "inv-key", label: "Key", quantity: 1 }] }),
      serverTimeUtc: new Date().toISOString(),
    });

    await runSessionRecovery(deps);

    expect(deps.requestSnapshot).toHaveBeenCalledTimes(1);
    expect(deps.applySnapshot).toHaveBeenCalledTimes(1);
    expect(deps.setConnectionError).not.toHaveBeenCalled();
  });

  it("propagates snapshot failure and sets connection error", async () => {
    const deps = buildDependencies();
    deps.recoverSession.mockRejectedValue(new Error("recover failed"));
    deps.requestSnapshot.mockRejectedValue(new Error("snapshot failed"));

    await expect(runSessionRecovery(deps)).rejects.toThrow("snapshot failed");
    expect(deps.setConnectionError).toHaveBeenCalledWith("snapshot failed");
  });
});
