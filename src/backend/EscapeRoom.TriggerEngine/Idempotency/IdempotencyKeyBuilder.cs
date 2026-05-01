using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Triggering.Contracts;

namespace EscapeRoom.TriggerEngine.Idempotency;

public class IdempotencyKeyBuilder
{
    public string Build(Guid sessionId, TriggerNodeDefinition node, PlayerActionEnvelope action)
    {
        var mode = node.Policy.Mode?.Trim().ToLowerInvariant() ?? "one-shot";
        if (mode == "repeatable")
        {
            var window = Math.Max(1, node.Policy.KeyWindowSeconds ?? 30);
            var bucket = new DateTimeOffset(action.TimestampUtc).ToUnixTimeSeconds() / window;
            return $"idempotency:{sessionId}:{node.NodeId}:{action.ClientActionId}:{bucket}";
        }

        return $"idempotency:{sessionId}:{node.NodeId}:{action.ClientActionId}";
    }

    public TimeSpan ResolveTtl(TriggerNodeDefinition node)
    {
        var mode = node.Policy.Mode?.Trim().ToLowerInvariant() ?? "one-shot";
        if (mode == "repeatable")
        {
            return TimeSpan.FromSeconds(Math.Max(1, node.Policy.KeyWindowSeconds ?? 30));
        }

        return TimeSpan.FromHours(24);
    }
}
