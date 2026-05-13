import { create } from "zustand";
import { SessionSnapshotEnvelope, StateDiffEnvelope } from "../types/realtime";
import {
  GameStateData,
  InventoryItem,
  RoomAsset,
  RoomHotspot,
  RoomLayer,
  RoomObjectState,
  RoomState,
} from "../types/gameState";

export type { GameStateData, InventoryItem, RoomAsset, RoomHotspot, RoomLayer, RoomObjectState, RoomState };

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
    backgroundColor: "#0b1220",
    assets: [],
    layers: [],
    hotspots: [],
    objectStates: [],
  },
  inventory: [
    {
      id: "inv-flashlight",
      label: "Flashlight",
      quantity: 1,
      type: "tool",
      stack: false,
      status: "ready",
    },
  ],
  clues: [],
  messages: ["Join a session to start receiving server state diffs."],
};

const asString = (value: unknown): string | null => (typeof value === "string" && value.trim() ? value : null);
const asOptionalString = (value: unknown): string | undefined => asString(value) ?? undefined;
const asNumber = (value: unknown): number | undefined => {
  const parsed = typeof value === "number" ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
};

const getRecordValue = (record: Record<string, unknown>, camelKey: string, pascalKey: string = camelKey[0].toUpperCase() + camelKey.slice(1)): unknown =>
  record[camelKey] ?? record[pascalKey];

const asStringArray = (value: unknown): string[] | undefined => {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const entries = value.filter((entry): entry is string => typeof entry === "string" && entry.trim().length > 0);
  return entries.length > 0 ? entries : undefined;
};

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
      type: "generic",
      stack: false,
      status: "ready",
    };
  }

  if (!value || typeof value !== "object") {
    return null;
  }

  const record = value as Record<string, unknown>;
  const label =
    asString(getRecordValue(record, "label")) ??
    asString(getRecordValue(record, "name"));
  if (!label) {
    return null;
  }

  const quantityValue = getRecordValue(record, "quantity");
  const quantityRaw = typeof quantityValue === "number" ? quantityValue : Number(quantityValue);
  const quantity = Number.isFinite(quantityRaw) ? Math.max(1, Math.floor(quantityRaw)) : 1;
  const explicitId = asString(getRecordValue(record, "id"));
  const type = asString(getRecordValue(record, "type")) ?? "generic";
  const stackValue = getRecordValue(record, "stack");
  const stack = typeof stackValue === "boolean" ? stackValue : quantity > 1;
  const status = asString(getRecordValue(record, "status")) ?? "ready";
  const usableTargetIds = asStringArray(getRecordValue(record, "usableTargetIds"));
  const combinableWithIds = asStringArray(getRecordValue(record, "combinableWithIds"));

  return {
    id: explicitId ?? `inv-${index}-${label.toLowerCase().replace(/\s+/g, "-")}`,
    label,
    quantity,
    type,
    stack,
    status,
    usableTargetIds,
    combinableWithIds,
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

const toHotspot = (value: Record<string, unknown>, defaults?: RoomHotspot): RoomHotspot | null => {
  const id = asString(getRecordValue(value, "id")) ?? defaults?.id;
  if (!id) {
    return null;
  }

  return {
    id,
    name: asString(getRecordValue(value, "name")) ?? defaults?.name ?? id,
    visualKind: asString(getRecordValue(value, "visualKind")) ?? defaults?.visualKind,
    variant: asString(getRecordValue(value, "variant")) ?? defaults?.variant,
    x: typeof getRecordValue(value, "x") === "number" ? (getRecordValue(value, "x") as number) : defaults?.x ?? 0,
    y: typeof getRecordValue(value, "y") === "number" ? (getRecordValue(value, "y") as number) : defaults?.y ?? 0,
    width: typeof getRecordValue(value, "width") === "number" ? (getRecordValue(value, "width") as number) : defaults?.width ?? 80,
    height: typeof getRecordValue(value, "height") === "number" ? (getRecordValue(value, "height") as number) : defaults?.height ?? 40,
    color: asString(getRecordValue(value, "color")) ?? defaults?.color ?? "#94a3b8",
    available: typeof getRecordValue(value, "available") === "boolean" ? (getRecordValue(value, "available") as boolean) : defaults?.available ?? true,
    visible: typeof getRecordValue(value, "visible") === "boolean" ? (getRecordValue(value, "visible") as boolean) : defaults?.visible ?? true,
    locked: typeof getRecordValue(value, "locked") === "boolean" ? (getRecordValue(value, "locked") as boolean) : defaults?.locked ?? false,
    interactive:
      typeof getRecordValue(value, "interactive") === "boolean"
        ? (getRecordValue(value, "interactive") as boolean)
        : defaults?.interactive ?? true,
    hitArea: getRecordValue(value, "hitArea") === "ellipse" ? "ellipse" : defaults?.hitArea ?? "rect",
    layerId: asString(getRecordValue(value, "layerId")) ?? defaults?.layerId,
    objectId: asString(getRecordValue(value, "objectId")) ?? defaults?.objectId,
    targetableItemIds: asStringArray(getRecordValue(value, "targetableItemIds")) ?? defaults?.targetableItemIds,
    targetableModes: Array.isArray(getRecordValue(value, "targetableModes"))
      ? (getRecordValue(value, "targetableModes") as unknown[]).filter(
          (entry): entry is "use" | "combine" | "inspect" | "pickup" =>
            entry === "use" || entry === "combine" || entry === "inspect" || entry === "pickup"
        )
      : defaults?.targetableModes,
  };
};

const toLayer = (value: Record<string, unknown>, defaults?: RoomLayer): RoomLayer | null => {
  const id = asString(value.id) ?? defaults?.id;
  if (!id) {
    return null;
  }

  return {
    id,
    name: asString(value.name) ?? defaults?.name ?? id,
    visualKind: asString(value.visualKind) ?? defaults?.visualKind,
    zIndex: typeof value.zIndex === "number" ? value.zIndex : defaults?.zIndex ?? 0,
    visible: typeof value.visible === "boolean" ? value.visible : defaults?.visible ?? true,
    opacity:
      typeof value.opacity === "number" && Number.isFinite(value.opacity)
        ? Math.max(0, Math.min(1, value.opacity))
        : defaults?.opacity ?? 1,
    color: asString(value.color) ?? defaults?.color,
    assetId: asString(value.assetId) ?? defaults?.assetId,
    objectId: asString(value.objectId) ?? defaults?.objectId,
  };
};

const toAsset = (value: Record<string, unknown>, defaults?: RoomAsset): RoomAsset | null => {
  const id = asString(value.id) ?? defaults?.id;
  if (!id) {
    return null;
  }

  const kind =
    value.kind === "background" || value.kind === "sprite" || value.kind === "overlay"
      ? value.kind
      : defaults?.kind ?? "sprite";

  return {
    id,
    kind,
    visualKind: asString(value.visualKind) ?? defaults?.visualKind,
    variant: asString(value.variant) ?? defaults?.variant,
    x: typeof value.x === "number" ? value.x : defaults?.x ?? 0,
    y: typeof value.y === "number" ? value.y : defaults?.y ?? 0,
    width: typeof value.width === "number" ? value.width : defaults?.width ?? 0,
    height: typeof value.height === "number" ? value.height : defaults?.height ?? 0,
    zIndex: typeof value.zIndex === "number" ? value.zIndex : defaults?.zIndex ?? 0,
    visible: typeof value.visible === "boolean" ? value.visible : defaults?.visible ?? true,
    opacity:
      typeof value.opacity === "number" && Number.isFinite(value.opacity)
        ? Math.max(0, Math.min(1, value.opacity))
        : defaults?.opacity ?? 1,
    color: asString(value.color) ?? defaults?.color,
    assetUrl: asString(value.assetUrl) ?? defaults?.assetUrl,
    objectId: asString(value.objectId) ?? defaults?.objectId,
  };
};

const toObjectState = (value: Record<string, unknown>, defaults?: RoomObjectState): RoomObjectState | null => {
  const id = asString(getRecordValue(value, "id")) ?? defaults?.id;
  if (!id) {
    return null;
  }

  return {
    id,
    visible: typeof getRecordValue(value, "visible") === "boolean" ? (getRecordValue(value, "visible") as boolean) : defaults?.visible ?? true,
    available:
      typeof getRecordValue(value, "available") === "boolean" ? (getRecordValue(value, "available") as boolean) : defaults?.available ?? true,
    locked: typeof getRecordValue(value, "locked") === "boolean" ? (getRecordValue(value, "locked") as boolean) : defaults?.locked ?? false,
    interactive:
      typeof getRecordValue(value, "interactive") === "boolean"
        ? (getRecordValue(value, "interactive") as boolean)
        : defaults?.interactive ?? true,
  };
};

const normalizeRoomState = (value: unknown, fallback: RoomState = initialGameData.room): RoomState => {
  if (!value || typeof value !== "object") {
    return fallback;
  }

  const room = value as Record<string, unknown>;

  const normalizedHotspots = Array.isArray(room.hotspots)
    ? room.hotspots
        .map((entry) => (entry && typeof entry === "object" ? toHotspot(entry as Record<string, unknown>) : null))
        .filter((entry): entry is RoomHotspot => entry !== null)
    : [];

  const legacyHotspots = Array.isArray(room.interactables)
    ? room.interactables
        .map((entry) => {
          if (!entry || typeof entry !== "object") {
            return null;
          }

          const legacy = entry as Record<string, unknown>;
          return toHotspot(
            {
              ...legacy,
              locked: typeof legacy.locked === "boolean" ? legacy.locked : false,
              interactive: typeof legacy.interactive === "boolean" ? legacy.interactive : true,
            },
            undefined
          );
        })
        .filter((entry): entry is RoomHotspot => entry !== null)
    : [];

  const hotspots = normalizedHotspots.length > 0 ? normalizedHotspots : legacyHotspots.length > 0 ? legacyHotspots : fallback.hotspots;

  const objectStates = Array.isArray(room.objectStates)
    ? room.objectStates
        .map((entry) => (entry && typeof entry === "object" ? toObjectState(entry as Record<string, unknown>) : null))
        .filter((entry): entry is RoomObjectState => entry !== null)
    : hotspots.map((hotspot) => ({
        id: hotspot.objectId ?? hotspot.id,
        visible: hotspot.visible,
        available: hotspot.available,
        locked: hotspot.locked,
        interactive: hotspot.interactive,
      }));

  const layers = Array.isArray(room.layers)
    ? room.layers
        .map((entry) => (entry && typeof entry === "object" ? toLayer(entry as Record<string, unknown>) : null))
        .filter((entry): entry is RoomLayer => entry !== null)
    : fallback.layers;

  const assets = Array.isArray(room.assets)
    ? room.assets
        .map((entry) => (entry && typeof entry === "object" ? toAsset(entry as Record<string, unknown>) : null))
        .filter((entry): entry is RoomAsset => entry !== null)
    : fallback.assets;

  return {
    roomName: asString(getRecordValue(room, "roomName")) ?? fallback.roomName,
    themeId: asOptionalString(getRecordValue(room, "themeId")) ?? fallback.themeId,
    width: typeof getRecordValue(room, "width") === "number" ? (getRecordValue(room, "width") as number) : fallback.width,
    height: typeof getRecordValue(room, "height") === "number" ? (getRecordValue(room, "height") as number) : fallback.height,
    backgroundColor: asString(getRecordValue(room, "backgroundColor")) ?? fallback.backgroundColor,
    hotspots,
    layers,
    assets,
    objectStates,
  };
};

const normalizeClues = (value: unknown): string[] => {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.filter((entry): entry is string => typeof entry === "string" && entry.trim().length > 0);
};

const normalizeSessionState = (value: unknown): GameStateData["session"] | undefined => {
  if (!value || typeof value !== "object") {
    return undefined;
  }

  const record = value as Record<string, unknown>;
  return {
    sessionId: asOptionalString(getRecordValue(record, "sessionId")),
    roomId: asOptionalString(getRecordValue(record, "roomId")),
    roomName: asOptionalString(getRecordValue(record, "roomName")),
    status: asOptionalString(getRecordValue(record, "status")),
    durationMinutes: asNumber(getRecordValue(record, "durationMinutes")),
    startedAtUtc: asOptionalString(getRecordValue(record, "startedAtUtc")),
    endedAtUtc: asString(getRecordValue(record, "endedAtUtc")),
    endsAtUtc: asString(getRecordValue(record, "endsAtUtc")),
    serverTimeUtc: asOptionalString(getRecordValue(record, "serverTimeUtc")),
    remainingSeconds: asNumber(getRecordValue(record, "remainingSeconds")),
    isQuickPlay: typeof getRecordValue(record, "isQuickPlay") === "boolean" ? (getRecordValue(record, "isQuickPlay") as boolean) : undefined,
    joinMode: asOptionalString(getRecordValue(record, "joinMode")),
    canSubmitActions:
      typeof getRecordValue(record, "canSubmitActions") === "boolean"
        ? (getRecordValue(record, "canSubmitActions") as boolean)
        : undefined,
  };
};

const mergeById = <T extends { id: string }>(
  current: T[],
  patches: Array<Record<string, unknown>>,
  factory: (patch: Record<string, unknown>, defaults?: T) => T | null
): T[] => {
  const byId = new Map(current.map((entry) => [entry.id, entry]));

  for (const patch of patches) {
    const id = asString(patch.id);
    if (!id) {
      continue;
    }

    const next = factory(patch, byId.get(id));
    if (!next) {
      continue;
    }

    byId.set(id, next);
  }

  return Array.from(byId.values());
};

const applyRoomPatch = (
  current: RoomState,
  roomPatch: NonNullable<StateDiffEnvelope["statePatch"]>["room"] | undefined
): RoomState => {
  if (!roomPatch) {
    return current;
  }

  const next: RoomState = {
    ...current,
    roomName: roomPatch.roomName ?? current.roomName,
    themeId: asString((roomPatch as Record<string, unknown>).themeId) ?? current.themeId,
    width: roomPatch.width ?? current.width,
    height: roomPatch.height ?? current.height,
    backgroundColor: asString(roomPatch.backgroundColor) ?? current.backgroundColor,
  };

  if (Array.isArray(roomPatch.hotspots)) {
    next.hotspots = mergeById(current.hotspots, roomPatch.hotspots as Array<Record<string, unknown>>, toHotspot);
  } else if (Array.isArray(roomPatch.interactables)) {
    next.hotspots = mergeById(current.hotspots, roomPatch.interactables as Array<Record<string, unknown>>, toHotspot);
  }

  if (Array.isArray(roomPatch.layers)) {
    next.layers = mergeById(current.layers, roomPatch.layers as Array<Record<string, unknown>>, toLayer);
  }

  if (Array.isArray(roomPatch.assets)) {
    next.assets = mergeById(current.assets, roomPatch.assets as Array<Record<string, unknown>>, toAsset);
  }

  if (Array.isArray(roomPatch.objectStates)) {
    next.objectStates = mergeById(
      current.objectStates,
      roomPatch.objectStates as Array<Record<string, unknown>>,
      toObjectState
    );
  }

  const objectStateById = new Map(next.objectStates.map((entry) => [entry.id, entry]));
  next.hotspots = next.hotspots.map((hotspot) => {
    const state = objectStateById.get(hotspot.objectId ?? hotspot.id) ?? objectStateById.get(hotspot.id);
    if (!state) {
      return hotspot;
    }

    return {
      ...hotspot,
      visible: state.visible,
      available: state.available,
      locked: state.locked,
      interactive: state.interactive,
    };
  });

  return next;
};

const applyStatePatch = (current: GameStateData, diff: StateDiffEnvelope): GameStateData => {
  if (typeof diff.fullStateJson === "string" && diff.fullStateJson.trim()) {
    return parseStateJson(diff.fullStateJson);
  }

  if (!diff.statePatch) {
    return current;
  }

  const nextRoom = diff.statePatch.room ? applyRoomPatch(current.room, diff.statePatch.room) : current.room;

  const nextInventory = Array.isArray(diff.statePatch.inventory)
    ? normalizeInventory(diff.statePatch.inventory)
    : current.inventory;

  const nextClues = Array.isArray(diff.statePatch.clues) ? normalizeClues(diff.statePatch.clues) : current.clues;
  const nextSession = diff.statePatch.session ? normalizeSessionState(diff.statePatch.session) : current.session;

  const patchMessages = Array.isArray(diff.statePatch.messages)
    ? diff.statePatch.messages.filter((message): message is string => typeof message === "string")
    : [];

  return {
    room: nextRoom,
    inventory: nextInventory,
    clues: nextClues,
    messages: [...current.messages, ...patchMessages],
    session: nextSession,
  };
};

const parseStateJson = (stateJson: string): GameStateData => {
  try {
    const parsed = JSON.parse(stateJson) as Partial<GameStateData> & { room?: unknown };
    const messages = Array.isArray(parsed.messages)
      ? parsed.messages.filter((entry): entry is string => typeof entry === "string")
      : initialGameData.messages;

    return {
      room: normalizeRoomState(parsed.room, initialGameData.room),
      inventory: normalizeInventory(parsed.inventory),
      clues: normalizeClues(parsed.clues),
      messages,
      session: normalizeSessionState(parsed.session),
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
