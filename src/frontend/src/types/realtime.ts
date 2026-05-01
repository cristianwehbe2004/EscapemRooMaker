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
