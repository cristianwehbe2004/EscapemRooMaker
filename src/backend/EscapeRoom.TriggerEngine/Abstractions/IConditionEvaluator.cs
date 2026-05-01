using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.Abstractions;

public interface IConditionEvaluator
{
    bool Evaluate(EvaluationContext context, TriggerNodeDefinition node);
}
