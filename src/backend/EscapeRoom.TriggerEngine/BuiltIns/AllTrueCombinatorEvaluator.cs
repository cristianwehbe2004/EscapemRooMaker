using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class AllTrueCombinatorEvaluator : ICombinatorEvaluator
{
    public bool Evaluate(EvaluationContext context, TriggerNodeDefinition node, IReadOnlyList<bool> inputs)
        => inputs.Count == 0 || inputs.All(x => x);
}
