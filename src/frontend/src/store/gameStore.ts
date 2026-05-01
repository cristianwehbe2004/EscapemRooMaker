import { create } from "zustand";
import { SessionSnapshotEnvelope, StateDiffEnvelope } from "../types/realtime";

export interface InventoryItem {
  id: string;
  label: string;
  quantity: number;
}

export interface Interactable {
  id: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  color: string;
  available: boolean;
  visible?: boolean;
  assetUrl?: string;
}

export interface RoomState {
  roomName: string;
  width: number;
  height: number;
  interactables: Interactable[];
}

export interface GameStateData {
  room: RoomState;
  inventory: InventoryItem[];
  messages: string[];
}

export interface GameStoreState {
  sessionId: string | null;
  connected: boolean;
  syncState: SyncState;
  sessionVersion: number;
  lastKnownVersion: number;
  lastDiffSequence: number;
  state: GameStateData;
  applySnapshot: (snapshot: SessionSnapshotEnvelope) => void;
  applyDiff: (diff: StateDiffEnvelope) => void;
  setConnectionStatus: (status: { connected?: boolean }) => void;
  setSyncState: (syncState: SyncState) => void;
  setSessionId: (sessionId: string) => void;
  reset: () => void;
}

export type SyncState = "reconnecting" | "recovering" | "replaying" | "synced";

export const initialGameData: GameStateData = {
  room: {
    roomName: "Escape Room",
    width: 900,
    height: 600,
    interactables: [
      {
        id: "desk-note",
        name: "Desk Note",
        x: 120,
        y: 130,
        width: 110,
        height: 50,
        color: "#facc15",
        available: true,
      },
      {
        id: "rusty-key",
        name: "Rusty Key",
        x: 420,
        y: 280,
        width: 90,
        height: 40,
        color: "#94a3b8",
        available: true,
      },
      {
        id: "locked-chest",
        name: "Locked Chest",
        x: 650,
        y: 360,
        width: 140,
        height: 90,
        color: "#b45309",
        available: true,
      },
    ],
  },
  inventory: [
    {
      id: "inv-flashlight",
      label: "Flashlight",
      quantity: 1,
    },
  ],
  messages: ["Join a session to start receiving server state diffs."],
};

const asString = (value: unknown): string | null => (typeof value === "string" && value.trim() ? value : null);

const toInventoryItem = (value: unknown, index: number): InventoryItem | null => {
  if (typeof value === "string") {
    const label = value.trim();
    if (!label) {
      return null;
    }

    return {
      id: `inv-${index}-${label.toLowerCase().replace(/\s+/g, "-")}`,
      label,
      quantity: 1,
    };
  }

  if (!value || typeof value !== "object") {
    return null;
  }

  const record = value as Record<string, unknown>;
  const label = asString(record.label) ?? asString(record.name);
  if (!label) {
    return null;
  }

  const quantityRaw = typeof record.quantity === "number" ? record.quantity : Number(record.quantity);
  const quantity = Number.isFinite(quantityRaw) ? Math.max(1, Math.floor(quantityRaw)) : 1;
  const explicitId = asString(record.id);

  return {
    id: explicitId ?? `inv-${index}-${label.toLowerCase().replace(/\s+/g, "-")}`,
    label,
    quantity,
  };
};

const normalizeInventory = (value: unknown): InventoryItem[] => {
  if (!Array.isArray(value)) {
    return initialGameData.inventory;
  }

  const items = value
    .map((entry, index) => toInventoryItem(entry, index))
    .filter((entry): entry is InventoryItem => entry !== null);

  return items.length > 0 ? items : [];
};

const mergeInteractables = (
  currentInteractables: Interactable[],
  nextInteractables: Array<Record<string, unknown>>
): Interactable[] => {
  const byId = new Map(currentInteractables.map((entry) => [entry.id, entry]));

  for (const patch of nextInteractables) {
    const id = asString(patch.id);
    if (!id) {
      continue;
    }

    const current = byId.get(id);
    const base: Interactable =
      current ?? {
        id,
        name: id,
        x: 0,
        y: 0,
        width: 80,
        height: 40,
        color: "#94a3b8",
        available: true,
      };

    byId.set(id, {
      ...base,
      name: asString(patch.name) ?? base.name,
      x: typeof patch.x === "number" ? patch.x : base.x,
      y: typeof patch.y === "number" ? patch.y : base.y,
      width: typeof patch.width === "number" ? patch.width : base.width,
      height: typeof patch.height === "number" ? patch.height : base.height,
      color: asString(patch.color) ?? base.color,
      available: typeof patch.available === "boolean" ? patch.available : base.available,
      visible: typeof patch.visible === "boolean" ? patch.visible : base.visible,
      assetUrl: asString(patch.assetUrl) ?? base.assetUrl,
    });
  }

  return Array.from(byId.values());
};

const applyStatePatch = (current: GameStateData, diff: StateDiffEnvelope): GameStateData => {
  if (typeof diff.fullStateJson === "string" && diff.fullStateJson.trim()) {
    return parseStateJson(diff.fullStateJson);
  }

  if (!diff.statePatch) {
    return current;
  }

  const nextRoom = { ...current.room };
  if (diff.statePatch.room) {
    nextRoom.roomName = diff.statePatch.room.roomName ?? nextRoom.roomName;
    nextRoom.width = diff.statePatch.room.width ?? nextRoom.width;
    nextRoom.height = diff.statePatch.room.height ?? nextRoom.height;

    if (Array.isArray(diff.statePatch.room.interactables)) {
      nextRoom.interactables = mergeInteractables(
        current.room.interactables,
        diff.statePatch.room.interactables as Array<Record<string, unknown>>
      );
    }
  }

  const nextInventory = Array.isArray(diff.statePatch.inventory)
    ? normalizeInventory(diff.statePatch.inventory)
    : current.inventory;

  const patchMessages = Array.isArray(diff.statePatch.messages)
    ? diff.statePatch.messages.filter((message): message is string => typeof message === "string")
    : [];

  return {
    room: nextRoom,
    inventory: nextInventory,
    messages: [...current.messages, ...patchMessages],
  };
};

const parseStateJson = (stateJson: string): GameStateData => {
  try {
    const parsed = JSON.parse(stateJson) as Partial<GameStateData>;
    const messages = Array.isArray(parsed.messages)
      ? parsed.messages.filter((entry): entry is string => typeof entry === "string")
      : initialGameData.messages;

    return {
      room: {
        roomName: parsed.room?.roomName ?? initialGameData.room.roomName,
        width: parsed.room?.width ?? initialGameData.room.width,
        height: parsed.room?.height ?? initialGameData.room.height,
        interactables: parsed.room?.interactables ?? initialGameData.room.interactables,
      },
      inventory: normalizeInventory(parsed.inventory),
      messages,
    };
  } catch {
    return initialGameData;
  }
};

export const diffNeedsSnapshotResync = (diff: StateDiffEnvelope): boolean => {
  if (diff.fullStateJson || diff.statePatch) {
    return false;
  }

  if (diff.changedEntities.length === 0) {
    return false;
  }

  const messageOnlyPrefixes = ["message", "messages", "gm.hint", "gm.broadcast", "ui.message", "chat"];
  return diff.changedEntities.some((entity) => {
    const normalized = entity.trim().toLowerCase();
    if (!normalized) {
      return false;
    }

    return !messageOnlyPrefixes.some((prefix) => normalized.startsWith(prefix));
  });
};

export const useGameStore = create<GameStoreState>((set) => ({
  sessionId: null,
  connected: false,
  syncState: "synced",
  sessionVersion: 0,
  lastKnownVersion: 0,
  lastDiffSequence: 0,
  state: initialGameData,
  applySnapshot: (snapshot) =>
    set((current) => ({
      ...current,
      sessionId: snapshot.sessionId,
      sessionVersion: snapshot.sessionVersion,
      lastKnownVersion: Math.max(current.lastKnownVersion, snapshot.sessionVersion),
      syncState: "synced",
      state: parseStateJson(snapshot.stateJson),
    })),
  applyDiff: (diff) =>
    set((current) => {
      if (diff.diffSequence <= current.lastDiffSequence) {
        return current;
      }

      const patchedState = applyStatePatch(current.state, diff);

      return {
        ...current,
        sessionVersion: Math.max(current.sessionVersion, diff.sessionVersion),
        lastKnownVersion: Math.max(current.lastKnownVersion, diff.sessionVersion),
        lastDiffSequence: diff.diffSequence,
        state: {
          ...patchedState,
          messages: [...patchedState.messages, ...diff.emittedMessages],
        },
      };
    }),
  setConnectionStatus: ({ connected }) =>
    set((current) => ({
      ...current,
      connected: connected ?? current.connected,
    })),
  setSyncState: (syncState) => set((current) => ({ ...current, syncState })),
  setSessionId: (sessionId) => set(() => ({ sessionId })),
  reset: () =>
    set(() => ({
      sessionId: null,
      connected: false,
      syncState: "synced",
      sessionVersion: 0,
      lastKnownVersion: 0,
      lastDiffSequence: 0,
      state: initialGameData,
    })),
}));
