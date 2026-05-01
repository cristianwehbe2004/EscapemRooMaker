export type ActionErrorSource = "local-cooldown" | "server-rate-limit" | "server" | "network";

export interface ActionError {
  source: ActionErrorSource;
  message: string;
  retryAfterMs?: number;
  policyName?: string;
  actionKey?: string;
}

const toMessage = (error: unknown): string => {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  if (typeof error === "string") {
    return error;
  }

  if (error && typeof error === "object" && "message" in error) {
    const value = (error as { message?: unknown }).message;
    if (typeof value === "string") {
      return value;
    }
  }

  return "Unexpected action error.";
};

const toRecord = (error: unknown): Record<string, unknown> | null =>
  error && typeof error === "object" ? (error as Record<string, unknown>) : null;

const parseStructuredPayload = (message: string): Record<string, unknown> | null => {
  const trimmed = message.trim();
  if (!trimmed.startsWith("{")) {
    return null;
  }

  try {
    const parsed = JSON.parse(trimmed);
    return parsed && typeof parsed === "object" ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
};

const parseRetryAfterMs = (record: Record<string, unknown> | null, message: string): number | undefined => {
  const directMs = record?.retryAfterMs;
  if (typeof directMs === "number" && Number.isFinite(directMs)) {
    return Math.max(0, Math.floor(directMs));
  }

  const directSeconds = record?.retryAfterSeconds;
  if (typeof directSeconds === "number" && Number.isFinite(directSeconds)) {
    return Math.max(0, Math.floor(directSeconds * 1000));
  }

  const secondsMatch = message.match(/retry[- ]?after\s*[:=]?\s*(\d+)\s*s/i);
  if (secondsMatch) {
    return Math.max(0, Number.parseInt(secondsMatch[1], 10) * 1000);
  }

  const msMatch = message.match(/retry[- ]?after\s*[:=]?\s*(\d+)\s*ms/i);
  if (msMatch) {
    return Math.max(0, Number.parseInt(msMatch[1], 10));
  }

  return undefined;
};

const parsePolicyName = (record: Record<string, unknown> | null, message: string): string | undefined => {
  const direct = record?.policyName;
  if (typeof direct === "string" && direct.trim()) {
    return direct;
  }

  const match = message.match(/policy\s*[:=]?\s*([a-z0-9_.-]+)/i);
  return match?.[1];
};

export const parseActionError = (error: unknown, actionKey?: string): ActionError => {
  const directRecord = toRecord(error);
  const rawMessage = toMessage(error);
  const payloadRecord = parseStructuredPayload(rawMessage);
  const record = payloadRecord ?? directRecord;
  const messageField = typeof record?.message === "string" && record.message.trim().length > 0 ? record.message : rawMessage;
  const normalized = messageField.toLowerCase();
  const code = typeof record?.code === "string" ? record.code.toLowerCase() : null;

  const retryAfterMs = parseRetryAfterMs(record, messageField);
  const policyName = parsePolicyName(record, messageField);

  const isRateLimited =
    code === "rate_limited" ||
    (normalized.includes("rate") && normalized.includes("limit")) ||
    normalized.includes("too many") ||
    normalized.includes("429") ||
    typeof retryAfterMs === "number";

  if (isRateLimited) {
    return {
      source: "server-rate-limit",
      message: messageField,
      retryAfterMs,
      policyName,
      actionKey,
    };
  }

  if (normalized.includes("network") || normalized.includes("connection") || normalized.includes("timeout")) {
    return {
      source: "network",
      message: messageField,
      actionKey,
    };
  }

  return {
    source: "server",
    message: messageField,
    retryAfterMs,
    policyName,
    actionKey,
  };
};
