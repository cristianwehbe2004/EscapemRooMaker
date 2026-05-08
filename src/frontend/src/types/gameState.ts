export type InventoryItem = {
  id: string;
  label: string;
  quantity: number;
  type: string;
  stack: boolean;
  status: string;
  usableTargetIds?: string[];
  combinableWithIds?: string[];
};

export type RoomAsset = {
  id: string;
  kind: "background" | "sprite" | "overlay";
  visualKind?: string;
  variant?: string;
  x: number;
  y: number;
  width: number;
  height: number;
  zIndex: number;
  visible: boolean;
  opacity: number;
  color?: string;
  assetUrl?: string;
  objectId?: string;
};

export type RoomLayer = {
  id: string;
  name: string;
  visualKind?: string;
  zIndex: number;
  visible: boolean;
  opacity: number;
  color?: string;
  assetId?: string;
  objectId?: string;
};

export type RoomObjectState = {
  id: string;
  visible: boolean;
  available: boolean;
  locked: boolean;
  interactive: boolean;
};

export type RoomHotspot = {
  id: string;
  name: string;
  visualKind?: string;
  variant?: string;
  x: number;
  y: number;
  width: number;
  height: number;
  color: string;
  visible: boolean;
  available: boolean;
  locked: boolean;
  interactive: boolean;
  hitArea?: "rect" | "ellipse";
  layerId?: string;
  objectId?: string;
  targetableItemIds?: string[];
  targetableModes?: Array<"use" | "combine" | "inspect" | "pickup">;
};

export type RoomState = {
  roomName: string;
  themeId?: string;
  width: number;
  height: number;
  backgroundColor: string;
  assets: RoomAsset[];
  layers: RoomLayer[];
  hotspots: RoomHotspot[];
  objectStates: RoomObjectState[];
};

export type GameStateData = {
  room: RoomState;
  inventory: InventoryItem[];
  clues?: string[];
  messages: string[];
  session?: {
    sessionId?: string;
    roomId?: string;
    roomName?: string;
    status?: string;
    durationMinutes?: number;
    startedAtUtc?: string;
    endedAtUtc?: string | null;
    endsAtUtc?: string | null;
    serverTimeUtc?: string;
    remainingSeconds?: number;
    isQuickPlay?: boolean;
    joinMode?: string;
    canSubmitActions?: boolean;
  };
};
