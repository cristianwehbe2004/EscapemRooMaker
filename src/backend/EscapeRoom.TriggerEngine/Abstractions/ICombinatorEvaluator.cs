using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.Abstractions;

public interface ICombinatorEvaluator
{
    bool Evaluate(EvaluationContext context, TriggerNodeDefinition node, IReadOnlyList<bool> inputs);
}
