namespace EscapeRoom.Realtime.RateLimiting;

public readonly record struct ActionRateLimitDecision(
    bool Allowed,
    int RetryAfterMs,
    string PolicyName,
    string PolicyScope,
    string ActionKey);
