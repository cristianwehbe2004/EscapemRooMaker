using System.Collections.Concurrent;
using EscapeRoom.Application.Realtime.Contracts;
using Microsoft.Extensions.Options;

namespace EscapeRoom.Realtime.RateLimiting;

public class InMemoryActionRateLimiter(IOptions<ActionRateLimitOptions> options) : IActionRateLimiter
{
    private readonly ConcurrentDictionary<string, long> expiryByKey = new(StringComparer.Ordinal);

    public ActionRateLimitDecision Evaluate(Guid sessionId, PlayerActionEnvelope action, ActionRateLimitContext context)
    {
        var configured = options.Value;
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var scope = ResolveScope(action, context);
        var policy = ResolvePolicy(configured, scope);
        var policyName = string.IsNullOrWhiteSpace(policy.PolicyName)
            ? scope == "gm" ? "gm-action-default" : "player-action-default"
            : policy.PolicyName;
        var cooldownMs = policy.Enabled ? Math.Max(0, policy.CooldownMs) : 0;
        var actorRole = string.IsNullOrWhiteSpace(context.ActorRole) ? "player" : context.ActorRole.Trim().ToLowerInvariant();

        var targetPart = action.Target ?? "";
        var actorPart = string.IsNullOrWhiteSpace(action.Actor) ? "unknown" : action.Actor.Trim();
        var actionType = string.IsNullOrWhiteSpace(action.ActionType) ? "unknown" : action.ActionType.Trim().ToLowerInvariant();
        var key = $"{scope}:{sessionId:N}:{actorRole}:{actorPart}:{actionType}:{targetPart}";

        if (!policy.Enabled || cooldownMs == 0)
        {
            return new ActionRateLimitDecision(true, 0, policyName, scope, key);
        }

        while (true)
        {
            if (!expiryByKey.TryGetValue(key, out var existingExpiresAt))
            {
                if (expiryByKey.TryAdd(key, nowMs + cooldownMs))
                {
                    return new ActionRateLimitDecision(true, 0, policyName, scope, key);
                }

                continue;
            }

            if (existingExpiresAt > nowMs)
            {
                return new ActionRateLimitDecision(
                    false,
                    (int)Math.Clamp(existingExpiresAt - nowMs, 0, int.MaxValue),
                    policyName,
                    scope,
                    key);
            }

            var updatedExpiresAt = nowMs + cooldownMs;
            if (expiryByKey.TryUpdate(key, updatedExpiresAt, existingExpiresAt))
            {
                CleanupIfNeeded(nowMs);
                return new ActionRateLimitDecision(true, 0, policyName, scope, key);
            }
        }
    }

    private static string ResolveScope(PlayerActionEnvelope action, ActionRateLimitContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.PolicyScope))
        {
            return context.PolicyScope.Trim().ToLowerInvariant();
        }

        return action.ActionType.Trim().StartsWith("gm.", StringComparison.OrdinalIgnoreCase) ? "gm" : "player";
    }

    private static ActionRateLimitPolicyOptions ResolvePolicy(ActionRateLimitOptions options, string scope)
        => scope switch
        {
            "gm" => options.Gm,
            _ => new ActionRateLimitPolicyOptions
            {
                Enabled = options.Player.Enabled,
                CooldownMs = options.Player.CooldownMs != 0 ? options.Player.CooldownMs : options.CooldownMs,
                PolicyName = string.IsNullOrWhiteSpace(options.Player.PolicyName) ? options.PolicyName : options.Player.PolicyName
            }
        };

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
