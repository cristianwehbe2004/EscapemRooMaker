export interface StateDiffEnvelope {
  sessionVersion: number;
  diffSequence: number;
  correlationId: string;
  emittedAtUtc: string;
  changedEntities: string[];
  emittedMessages: string[];
  appliedEffects: string[];
  statePatch?: StatePatchEnvelope;
  fullStateJson?: string;
}

export interface StatePatchEnvelope {
  room?: {
    roomName?: string;
    width?: number;
    height?: number;
    backgroundColor?: string;
    assets?: Array<{
      id: string;
      kind?: "background" | "sprite" | "overlay";
      x?: number;
      y?: number;
      width?: number;
      height?: number;
      zIndex?: number;
      visible?: boolean;
      opacity?: number;
      color?: string;
      assetUrl?: string;
      objectId?: string;
    }>;
    layers?: Array<{
      id: string;
      name?: string;
      zIndex?: number;
      visible?: boolean;
      opacity?: number;
      color?: string;
      assetId?: string;
      objectId?: string;
    }>;
    hotspots?: Array<{
      id: string;
      name?: string;
      x?: number;
      y?: number;
      width?: number;
      height?: number;
      color?: string;
      available?: boolean;
      visible?: boolean;
      locked?: boolean;
      interactive?: boolean;
      hitArea?: "rect" | "ellipse";
      layerId?: string;
      objectId?: string;
      targetableItemIds?: string[];
      targetableModes?: Array<"use" | "combine" | "inspect" | "pickup">;
    }>;
    objectStates?: Array<{
      id: string;
      visible?: boolean;
      available?: boolean;
      locked?: boolean;
      interactive?: boolean;
    }>;
    interactables?: Array<{
      id: string;
      name?: string;
      x?: number;
      y?: number;
      width?: number;
      height?: number;
      color?: string;
      available?: boolean;
      visible?: boolean;
      assetUrl?: string;
      locked?: boolean;
      interactive?: boolean;
    }>;
  };
  inventory?: Array<string | { id?: string; label?: string; name?: string; quantity?: number }>;
  messages?: string[];
}

export interface SessionSnapshotEnvelope {
  sessionId: string;
  sessionVersion: number;
  stateJson: string;
  serverTimeUtc: string;
  playerPresence?: PlayerPresenceEvent[];
}

export interface PlayerActionEnvelope {
  actionType: string;
  actor: string;
  target?: string;
  payload: Record<string, unknown>;
  clientActionId: string;
  timestampUtc: string;
}

export interface JoinSessionAck {
  sessionId: string;
  replayedDiffCount: number;
  lastKnownVersion?: number | null;
  currentVersion: number;
}

export interface RecoverSessionResult {
  sessionId: string;
  replayedDiffCount: number;
  snapshotSent: boolean;
  currentVersion: number;
}

export interface GmHintAction {
  hint: string;
  scope: string;
  target?: string;
  clientActionId: string;
}

export interface GmControlAction {
  controlType: string;
  target?: string;
  payload: Record<string, unknown>;
  clientActionId: string;
}

export interface PlayerPresenceEvent {
  sessionId: string;
  playerId: string;
  displayName: string;
  status: string;
  isConnected: boolean;
  connectedAtUtc: string;
  lastSeenUtc: string;
}

export interface SessionTimelineEntry {
  sessionId: string;
  sequenceNumber: number;
  eventType: string;
  actor: string;
  target?: string;
  summary: string;
  occurredAtUtc: string;
}

export interface GmSessionSummary {
  sessionId: string;
  roomId: string;
  roomName: string;
  status: string;
  startedAtUtc: string;
  endedAtUtc?: string | null;
  connectedPlayers: number;
}
