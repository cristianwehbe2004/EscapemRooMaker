using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class ActionTypeConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(EvaluationContext context, TriggerNodeDefinition node)
    {
        if (!node.Config.TryGetValue("expectedActionType", out var expected) || expected is null)
        {
            return false;
        }

        return string.Equals(expected.ToString(), context.Action.ActionType, StringComparison.OrdinalIgnoreCase);
    }
}
