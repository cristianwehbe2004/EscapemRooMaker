using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.Realtime.RateLimiting;

public class ActionRateLimitError
{
    public string Code { get; set; } = "rate_limited";
    public string Message { get; set; } = "Action rate limited.";
    public int RetryAfterMs { get; set; }
    public string PolicyName { get; set; } = "player-action-default";
    public string? PolicyScope { get; set; }
    public string? ActionKey { get; set; }
    public string? ActionType { get; set; }
    public string? Target { get; set; }

    public static ActionRateLimitError FromDecision(ActionRateLimitDecision decision, PlayerActionEnvelope action) => new()
    {
        RetryAfterMs = decision.RetryAfterMs,
        PolicyName = decision.PolicyName,
        PolicyScope = decision.PolicyScope,
        ActionKey = decision.ActionKey,
        ActionType = action.ActionType,
        Target = action.Target,
        Message = $"Action rate limited. Retry after {decision.RetryAfterMs}ms.",
    };
}
