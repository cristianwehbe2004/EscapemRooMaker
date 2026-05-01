import { SessionSnapshotEnvelope, StateDiffEnvelope } from "../types/realtime";
import { diffNeedsSnapshotResync, initialGameData, useGameStore } from "./gameStore";

describe("gameStore", () => {
  beforeEach(() => {
    useGameStore.getState().reset();
  });

  it("applies a snapshot payload", () => {
    const snapshot: SessionSnapshotEnvelope = {
      sessionId: "session-1",
      sessionVersion: 5,
      stateJson: JSON.stringify({
        room: {
          roomName: "Basement",
          width: 640,
          height: 480,
          interactables: [{ id: "door", name: "Door", x: 10, y: 10, width: 90, height: 140, color: "#aaa", available: true }],
        },
        inventory: [{ id: "key-1", label: "Key", quantity: 1 }],
        messages: ["welcome"],
      }),
      serverTimeUtc: new Date().toISOString(),
    };

    useGameStore.getState().applySnapshot(snapshot);
    const state = useGameStore.getState();

    expect(state.sessionId).toBe("session-1");
    expect(state.sessionVersion).toBe(5);
    expect(state.state.room.roomName).toBe("Basement");
    expect(state.state.inventory).toEqual([{ id: "key-1", label: "Key", quantity: 1 }]);
  });

  it("ignores stale diff sequence and applies newer diff", () => {
    const staleDiff: StateDiffEnvelope = {
      sessionVersion: 2,
      diffSequence: 1,
      correlationId: "c1",
      emittedAtUtc: new Date().toISOString(),
      changedEntities: [],
      emittedMessages: ["old"],
      appliedEffects: [],
    };

    const newerDiff: StateDiffEnvelope = {
      ...staleDiff,
      diffSequence: 2,
      sessionVersion: 3,
      correlationId: "c2",
      emittedMessages: ["new"],
    };

    useGameStore.getState().applyDiff(newerDiff);
    useGameStore.getState().applyDiff(staleDiff);
    const state = useGameStore.getState();

    expect(state.lastDiffSequence).toBe(2);
    expect(state.sessionVersion).toBe(3);
    expect(state.state.messages).toEqual([
      "Join a session to start receiving server state diffs.",
      "new",
    ]);
  });

  it("resets to initial state", () => {
    useGameStore.getState().setSessionId("abc");
    useGameStore.getState().reset();
    const state = useGameStore.getState();

    expect(state.sessionId).toBeNull();
    expect(state.state).toEqual(initialGameData);
  });

  it("applies inventory and room patch from diff", () => {
    const diff: StateDiffEnvelope = {
      sessionVersion: 2,
      diffSequence: 2,
      correlationId: "patch-1",
      emittedAtUtc: new Date().toISOString(),
      changedEntities: ["inventory", "room"],
      emittedMessages: [],
      appliedEffects: [],
      statePatch: {
        inventory: [{ id: "inv-key", label: "Rusty Key", quantity: 2 }],
        room: {
          interactables: [
            {
              id: "locked-chest",
              available: false,
              visible: false,
            },
          ],
        },
      },
    };

    useGameStore.getState().applyDiff(diff);
    const state = useGameStore.getState();

    expect(state.state.inventory).toEqual([{ id: "inv-key", label: "Rusty Key", quantity: 2 }]);
    const chest = state.state.room.interactables.find((entry) => entry.id === "locked-chest");
    expect(chest?.available).toBe(false);
    expect(chest?.visible).toBe(false);
  });

  it("flags non-message diffs without patch for snapshot resync", () => {
    const messageOnly: StateDiffEnvelope = {
      sessionVersion: 2,
      diffSequence: 3,
      correlationId: "msg-only",
      emittedAtUtc: new Date().toISOString(),
      changedEntities: ["messages"],
      emittedMessages: ["hello"],
      appliedEffects: [],
    };

    const stateOnly: StateDiffEnvelope = {
      ...messageOnly,
      correlationId: "state-only",
      changedEntities: ["state"],
    };

    expect(diffNeedsSnapshotResync(messageOnly)).toBe(false);
    expect(diffNeedsSnapshotResync(stateOnly)).toBe(true);
  });
});
