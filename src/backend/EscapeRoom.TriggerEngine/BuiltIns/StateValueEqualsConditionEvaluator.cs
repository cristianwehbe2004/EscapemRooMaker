using System.Text.Json;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class StateValueEqualsConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(EvaluationContext context, TriggerNodeDefinition node)
    {
        var key = JsonStateHelpers.GetConfigString(node, "key")
            ?? JsonStateHelpers.GetConfigString(node, "path");
        if (string.IsNullOrWhiteSpace(key) || !node.Config.TryGetValue("value", out var expected))
        {
            return false;
        }

        var actualNode = JsonStateHelpers.ResolvePath(context.State, key);
        if (actualNode is null)
        {
            return false;
        }

        var expectedNode = JsonStateHelpers.ToJsonNode(expected);
        return JsonSerializer.Serialize(actualNode) == JsonSerializer.Serialize(expectedNode);
    }
}
