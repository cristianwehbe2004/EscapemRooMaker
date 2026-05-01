using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.Realtime.RateLimiting;

public interface IActionRateLimiter
{
    ActionRateLimitDecision Evaluate(Guid sessionId, PlayerActionEnvelope action, ActionRateLimitContext context);
}
