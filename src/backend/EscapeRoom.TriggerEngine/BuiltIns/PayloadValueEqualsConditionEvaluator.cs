using System.Text.Json;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class PayloadValueEqualsConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(EvaluationContext context, TriggerNodeDefinition node)
    {
        var key = JsonStateHelpers.GetConfigString(node, "key") ?? JsonStateHelpers.GetConfigString(node, "payloadKey");
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!node.Config.TryGetValue("value", out var expectedValue))
        {
            return false;
        }

        if (!context.Action.Payload.TryGetValue(key, out var payloadValue))
        {
            return false;
        }

        return string.Equals(Normalize(payloadValue), Normalize(expectedValue), StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            string s => s,
            bool b => b.ToString(),
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element when element.ValueKind == JsonValueKind.True => true.ToString(),
            JsonElement element when element.ValueKind == JsonValueKind.False => false.ToString(),
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.ToString(),
            _ => value.ToString()
        };
    }
}
