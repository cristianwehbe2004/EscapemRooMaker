import { RoomState } from "./gameState";

export type TriggerGraphNode = {
  nodeId: string;
  family: "condition" | "combinator" | "effect";
  type: string;
  config: Record<string, unknown>;
  policy: {
    mode: string;
    keyWindowSeconds?: number | null;
  };
};

export type TriggerGraphEdge = {
  fromNodeId: string;
  toNodeId: string;
};

export type TriggerGraphDto = {
  version: number;
  metadata: Record<string, string>;
  nodes: TriggerGraphNode[];
  edges: TriggerGraphEdge[];
};

export type EditorDocumentDto = {
  room: RoomState;
  triggerGraph: TriggerGraphDto;
};

export type ValidationIssueDto = {
  code: string;
  message: string;
  path: string;
};

export type ValidateRoomResponse = {
  isValid: boolean;
  issues: ValidationIssueDto[];
};

export type SaveRoomResponse = {
  roomId: string;
  versionNumber: number;
  savedAtUtc: string;
  issues: ValidationIssueDto[];
};

export type CreatePlaytestSessionResponse = {
  sessionId: string;
  playerJoinPath: string;
  gmJoinPath: string;
};
