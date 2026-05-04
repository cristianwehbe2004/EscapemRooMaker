using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class CompleteSessionEffectExecutor : IEffectExecutor
{
    public EffectExecutionResult Execute(EvaluationContext context, TriggerNodeDefinition node)
    {
        var result = new EffectExecutionResult { Applied = true };
        var session = JsonStateHelpers.GetOrCreateObject(context.State, "session");
        session["status"] = "Completed";
        session["completedAtUtc"] = DateTime.UtcNow;

        var message = JsonStateHelpers.GetConfigString(node, "message");
        if (!string.IsNullOrWhiteSpace(message))
        {
            result.Messages.Add(message);
        }

        result.ChangedEntities.Add("session");
        return result;
    }
}
