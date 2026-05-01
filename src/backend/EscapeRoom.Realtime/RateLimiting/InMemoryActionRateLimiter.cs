using System.Collections.Concurrent;
using EscapeRoom.Application.Realtime.Contracts;
using Microsoft.Extensions.Options;

namespace EscapeRoom.Realtime.RateLimiting;

public class InMemoryActionRateLimiter(IOptions<ActionRateLimitOptions> options) : IActionRateLimiter
{
    private readonly ConcurrentDictionary<string, long> expiryByKey = new(StringComparer.Ordinal);

    public ActionRateLimitDecision Evaluate(Guid sessionId, PlayerActionEnvelope action)
    {
        var configured = options.Value;
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var policyName = string.IsNullOrWhiteSpace(configured.PolicyName) ? "player-action-default" : configured.PolicyName;
        var cooldownMs = Math.Max(0, configured.CooldownMs);

        if (cooldownMs == 0)
        {
            return new ActionRateLimitDecision(true, 0, policyName);
        }

        var targetPart = action.Target ?? "";
        var actorPart = string.IsNullOrWhiteSpace(action.Actor) ? "unknown" : action.Actor.Trim();
        var actionType = string.IsNullOrWhiteSpace(action.ActionType) ? "unknown" : action.ActionType.Trim().ToLowerInvariant();
        var key = $"{sessionId:N}:{actorPart}:{actionType}:{targetPart}";

        while (true)
        {
            if (!expiryByKey.TryGetValue(key, out var existingExpiresAt))
            {
                if (expiryByKey.TryAdd(key, nowMs + cooldownMs))
                {
                    return new ActionRateLimitDecision(true, 0, policyName);
                }

                continue;
            }

            if (existingExpiresAt > nowMs)
            {
                return new ActionRateLimitDecision(false, (int)Math.Clamp(existingExpiresAt - nowMs, 0, int.MaxValue), policyName);
            }

            var updatedExpiresAt = nowMs + cooldownMs;
            if (expiryByKey.TryUpdate(key, updatedExpiresAt, existingExpiresAt))
            {
                CleanupIfNeeded(nowMs);
                return new ActionRateLimitDecision(true, 0, policyName);
            }
        }
    }

    private void CleanupIfNeeded(long nowMs)
    {
        if (expiryByKey.Count < 1024)
        {
            return;
        }

        foreach (var entry in expiryByKey)
        {
            if (entry.Value <= nowMs)
            {
                expiryByKey.TryRemove(entry.Key, out _);
            }
        }
    }
}
