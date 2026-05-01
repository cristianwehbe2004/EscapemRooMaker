using EscapeRoom.Application.Triggering.Contracts;

namespace EscapeRoom.TriggerEngine.Evaluation;

public interface ITriggerGraphEvaluator
{
    Task<EvaluationResult> EvaluateAsync(TriggerGraphDefinition graph, EvaluationContext context, CancellationToken cancellationToken = default);
}
