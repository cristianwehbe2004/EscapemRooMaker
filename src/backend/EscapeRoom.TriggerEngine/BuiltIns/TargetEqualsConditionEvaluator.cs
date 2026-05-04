using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class TargetEqualsConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(EvaluationContext context, TriggerNodeDefinition node)
    {
        var expected = JsonStateHelpers.GetConfigString(node, "expectedTarget")
            ?? JsonStateHelpers.GetConfigString(node, "targetId");
        return !string.IsNullOrWhiteSpace(expected)
            && string.Equals(expected, context.Action.Target, StringComparison.OrdinalIgnoreCase);
    }
}
