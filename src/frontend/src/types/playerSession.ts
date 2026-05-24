export type CreateSessionRequest = {
  roomId?: string;
  durationMinutes?: number;
  displayName?: string;
  guestActorId?: string;
};

export type JoinSessionRequest = {
  displayName?: string;
  guestActorId?: string;
};

export type PlayerSessionSummary = {
  sessionId: string;
  roomId: string;
  roomName: string;
  status: string;
  durationMinutes: number;
  startedAtUtc: string;
  endedAtUtc?: string | null;
  endsAtUtc?: string | null;
  serverTimeUtc: string;
  remainingSeconds: number;
  isQuickPlay: boolean;
  playerJoinPath: string;
  gmJoinPath: string;
  actorId: string;
  displayName: string;
  isHost?: boolean;
  joinMode: "player" | "spectator" | string;
  canSubmitActions: boolean;
  participants?: PlayerSessionParticipant[];
};

export type PlayerSessionParticipant = {
  actorId: string;
  displayName: string;
  joinMode: "player" | "spectator" | string;
  canSubmitActions: boolean;
  isHost?: boolean;
  joinedAtUtc?: string | null;
  lastSeenAtUtc?: string | null;
};

export type KickSessionParticipantRequest = {
  targetActorId: string;
  displayName?: string;
  guestActorId?: string;
};
