import { SessionSnapshotEnvelope, StateDiffEnvelope } from "../types/realtime";
import { diffNeedsSnapshotResync, initialGameData, useGameStore } from "./gameStore";

describe("gameStore", () => {
  beforeEach(() => {
    useGameStore.getState().reset();
  });

  it("applies a snapshot payload with rich room state", () => {
    const snapshot: SessionSnapshotEnvelope = {
      sessionId: "session-1",
      sessionVersion: 5,
      stateJson: JSON.stringify({
        room: {
          roomName: "Basement",
          width: 640,
          height: 480,
          backgroundColor: "#111827",
          assets: [{ id: "bg", kind: "background", x: 0, y: 0, width: 640, height: 480, zIndex: 0, visible: true, opacity: 1 }],
          hotspots: [
            {
              id: "door",
              name: "Door",
              x: 10,
              y: 10,
              width: 90,
              height: 140,
              color: "#aaa",
              available: true,
              visible: true,
              locked: false,
              interactive: true,
            },
          ],
          objectStates: [{ id: "door", visible: true, available: true, locked: false, interactive: true }],
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
    expect(state.state.room.hotspots).toHaveLength(1);
    expect(state.state.inventory).toEqual([
      {
        id: "key-1",
        label: "Key",
        quantity: 1,
        type: "generic",
        stack: false,
        status: "ready",
        usableTargetIds: undefined,
        combinableWithIds: undefined,
      },
    ]);
  });

  it("maps legacy interactables from snapshot into hotspots", () => {
    const snapshot: SessionSnapshotEnvelope = {
      sessionId: "session-legacy",
      sessionVersion: 3,
      stateJson: JSON.stringify({
        room: {
          roomName: "Legacy Room",
          width: 500,
          height: 400,
          interactables: [{ id: "legacy-note", name: "Legacy Note", x: 5, y: 6, width: 50, height: 20, color: "#fff", available: true, visible: true }],
        },
      }),
      serverTimeUtc: new Date().toISOString(),
    };

    useGameStore.getState().applySnapshot(snapshot);
    const state = useGameStore.getState();

    expect(state.state.room.hotspots[0].id).toBe("legacy-note");
    expect(state.state.room.hotspots[0].interactive).toBe(true);
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
    expect(state.state.messages).toEqual(["Join a session to start receiving server state diffs.", "new"]);
  });

  it("resets to initial state", () => {
    useGameStore.getState().setSessionId("abc");
    useGameStore.getState().reset();
    const state = useGameStore.getState();

    expect(state.sessionId).toBeNull();
    expect(state.state).toEqual(initialGameData);
  });

  it("applies inventory and room mutation patches from diff", () => {
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
          objectStates: [{ id: "locked-chest", available: false, visible: false, locked: true, interactive: false }],
          hotspots: [{ id: "locked-chest", targetableItemIds: ["inv-key"], targetableModes: ["use"] }],
        },
      },
    };

    useGameStore.getState().applyDiff(diff);
    const state = useGameStore.getState();

    expect(state.state.inventory).toEqual([
      {
        id: "inv-key",
        label: "Rusty Key",
        quantity: 2,
        type: "generic",
        stack: true,
        status: "ready",
        usableTargetIds: undefined,
        combinableWithIds: undefined,
      },
    ]);
    const chest = state.state.room.hotspots.find((entry) => entry.id === "locked-chest");
    expect(chest?.available).toBe(false);
    expect(chest?.visible).toBe(false);
    expect(chest?.interactive).toBe(false);
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

  it("normalizes legacy and rich inventory payloads", () => {
    const snapshot: SessionSnapshotEnvelope = {
      sessionId: "session-rich-inventory",
      sessionVersion: 6,
      stateJson: JSON.stringify({
        room: initialGameData.room,
        inventory: [
          "Old Key",
          {
            id: "inv-crowbar",
            label: "Crowbar",
            quantity: 1,
            type: "tool",
            stack: false,
            status: "ready",
            usableTargetIds: ["locked-chest"],
            combinableWithIds: ["inv-tape"],
          },
        ],
      }),
      serverTimeUtc: new Date().toISOString(),
    };

    useGameStore.getState().applySnapshot(snapshot);
    const state = useGameStore.getState();

    expect(state.state.inventory[0]).toMatchObject({
      label: "Old Key",
      quantity: 1,
      type: "generic",
      status: "ready",
    });
    expect(state.state.inventory[1]).toEqual({
      id: "inv-crowbar",
      label: "Crowbar",
      quantity: 1,
      type: "tool",
      stack: false,
      status: "ready",
      usableTargetIds: ["locked-chest"],
      combinableWithIds: ["inv-tape"],
    });
  });
});
