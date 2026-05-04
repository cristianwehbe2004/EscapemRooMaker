using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class SetStateValueEffectExecutor : IEffectExecutor
{
    public EffectExecutionResult Execute(EvaluationContext context, TriggerNodeDefinition node)
    {
        var result = new EffectExecutionResult { Applied = true };
        if (!node.Config.TryGetValue("key", out var key) || key is null)
        {
            return result;
        }

        node.Config.TryGetValue("value", out var value);
        var stateKey = key.ToString() ?? string.Empty;
        JsonStateHelpers.SetPath(context.State, stateKey, JsonStateHelpers.ToJsonNode(value));
        result.ChangedEntities.Add("state");
        return result;
    }
}
