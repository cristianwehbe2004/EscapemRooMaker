import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import {
  GmControlAction,
  GmHintAction,
  GmSessionSummary,
  JoinSessionAck,
  PlayerPresenceEvent,
  PlayerActionEnvelope,
  RecoverSessionResult,
  SessionSnapshotEnvelope,
  SessionTimelineEntry,
  StateDiffEnvelope,
} from "../types/realtime";

type RealtimeHandlers = {
  onDiff: (diff: StateDiffEnvelope) => void;
  onSnapshot: (snapshot: SessionSnapshotEnvelope) => void;
  onPresenceChanged?: (event: PlayerPresenceEvent) => void;
  onReconnecting?: () => void;
  onReconnected?: (sessionId: string | null) => void;
  onDisconnected?: () => void;
};

type RealtimeClientOptions = {
  baseUrl: string;
  getAccessToken?: () => string | Promise<string>;
};

export class GameRealtimeClient {
  private readonly connection: HubConnection;
  private activeSessionId: string | null = null;

  constructor(options: RealtimeClientOptions, handlers: RealtimeHandlers) {
    this.connection = new HubConnectionBuilder()
      .withUrl(`${options.baseUrl}/hubs/game`, {
        accessTokenFactory: options.getAccessToken,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on("StateDiff", handlers.onDiff);
    this.connection.on("SessionSnapshot", handlers.onSnapshot);
    this.connection.on("PlayerPresenceChanged", (event: PlayerPresenceEvent) => {
      handlers.onPresenceChanged?.(event);
    });
    this.connection.onreconnecting(() => {
      handlers.onReconnecting?.();
    });
    this.connection.onreconnected(async () => {
      handlers.onReconnected?.(this.activeSessionId);
    });
    this.connection.onclose(() => {
      handlers.onDisconnected?.();
    });
  }

  async start(sessionId: string, lastKnownVersion?: number): Promise<JoinSessionAck> {
    this.activeSessionId = sessionId;
    await this.ensureStarted();

    return this.connection.invoke<JoinSessionAck>("JoinSession", sessionId, lastKnownVersion ?? null);
  }

  async stop(): Promise<void> {
    this.activeSessionId = null;
    if (this.connection.state !== "Disconnected") {
      await this.connection.stop();
    }
  }

  async submitAction(sessionId: string, action: PlayerActionEnvelope): Promise<StateDiffEnvelope> {
    await this.ensureStarted();
    return this.connection.invoke<StateDiffEnvelope>("SubmitAction", sessionId, action);
  }

  async requestSnapshot(sessionId: string): Promise<SessionSnapshotEnvelope> {
    await this.ensureStarted();
    return this.connection.invoke<SessionSnapshotEnvelope>("RequestSnapshot", sessionId);
  }

  async recoverSession(sessionId: string, lastKnownVersion: number): Promise<RecoverSessionResult> {
    await this.ensureStarted();
    return this.connection.invoke<RecoverSessionResult>("RecoverSession", sessionId, lastKnownVersion);
  }

  async submitGmHint(sessionId: string, hint: GmHintAction): Promise<StateDiffEnvelope> {
    await this.ensureStarted();
    return this.connection.invoke<StateDiffEnvelope>("SubmitGmHint", sessionId, hint);
  }

  async submitGmControl(sessionId: string, control: GmControlAction): Promise<StateDiffEnvelope> {
    await this.ensureStarted();
    return this.connection.invoke<StateDiffEnvelope>("SubmitGmControl", sessionId, control);
  }

  async forceSyncSession(sessionId: string): Promise<StateDiffEnvelope> {
    await this.ensureStarted();
    return this.connection.invoke<StateDiffEnvelope>("ForceSyncSession", sessionId);
  }

  async revealPuzzle(sessionId: string, puzzleId: string, target?: string): Promise<StateDiffEnvelope> {
    await this.ensureStarted();
    return this.connection.invoke<StateDiffEnvelope>("RevealPuzzle", sessionId, puzzleId, target ?? null);
  }

  async broadcastMessage(sessionId: string, message: string, target?: string): Promise<StateDiffEnvelope> {
    await this.ensureStarted();
    return this.connection.invoke<StateDiffEnvelope>("BroadcastMessage", sessionId, message, target ?? null);
  }

  async getActiveSessions(): Promise<GmSessionSummary[]> {
    await this.ensureStarted();
    return this.connection.invoke<GmSessionSummary[]>("GetActiveSessions");
  }

  async getSessionTimeline(sessionId: string, take = 120): Promise<SessionTimelineEntry[]> {
    await this.ensureStarted();
    return this.connection.invoke<SessionTimelineEntry[]>("GetSessionTimeline", sessionId, take);
  }

  async getPlayerPresence(sessionId: string): Promise<PlayerPresenceEvent[]> {
    await this.ensureStarted();
    return this.connection.invoke<PlayerPresenceEvent[]>("GetPlayerPresence", sessionId);
  }

  private async ensureStarted(): Promise<void> {
    if (this.connection.state === "Disconnected") {
      await this.connection.start();
    }
  }
}
