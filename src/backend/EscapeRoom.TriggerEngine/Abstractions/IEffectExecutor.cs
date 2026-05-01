using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.Abstractions;

public interface IEffectExecutor
{
    EffectExecutionResult Execute(EvaluationContext context, TriggerNodeDefinition node);
}

public class EffectExecutionResult
{
    public bool Applied { get; set; }
    public List<string> ChangedEntities { get; } = new();
    public List<string> Messages { get; } = new();
}
