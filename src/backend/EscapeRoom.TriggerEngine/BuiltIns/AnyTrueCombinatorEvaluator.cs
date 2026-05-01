using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class AnyTrueCombinatorEvaluator : ICombinatorEvaluator
{
    public bool Evaluate(EvaluationContext context, TriggerNodeDefinition node, IReadOnlyList<bool> inputs)
        => inputs.Any(x => x);
}
