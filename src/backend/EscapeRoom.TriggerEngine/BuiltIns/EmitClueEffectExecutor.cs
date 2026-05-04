using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class EmitClueEffectExecutor : IEffectExecutor
{
    public EffectExecutionResult Execute(EvaluationContext context, TriggerNodeDefinition node)
    {
        var result = new EffectExecutionResult { Applied = true };
        var clue = JsonStateHelpers.GetConfigString(node, "clue") ?? JsonStateHelpers.GetConfigString(node, "message");
        if (string.IsNullOrWhiteSpace(clue))
        {
            return result;
        }

        var clues = JsonStateHelpers.GetOrCreateArray(context.State, "clues");
        if (!clues.Any(entry => string.Equals(entry?.GetValue<string>(), clue, StringComparison.OrdinalIgnoreCase)))
        {
            clues.Add(clue);
        }

        result.Messages.Add($"Clue: {clue}");
        result.ChangedEntities.Add("clues");
        return result;
    }
}
