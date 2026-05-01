using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.BuiltIns;

namespace EscapeRoom.TriggerEngine.Registry;

public class CombinatorRegistry(
    AllTrueCombinatorEvaluator allTrueCombinatorEvaluator,
    AnyTrueCombinatorEvaluator anyTrueCombinatorEvaluator) : ICombinatorRegistry
{
    private readonly Dictionary<string, ICombinatorEvaluator> _evaluators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["allTrue"] = allTrueCombinatorEvaluator,
        ["anyTrue"] = anyTrueCombinatorEvaluator
    };

    public ICombinatorEvaluator Get(string type)
    {
        if (_evaluators.TryGetValue(type, out var evaluator))
        {
            return evaluator;
        }

        throw new InvalidOperationException($"Combinator evaluator '{type}' is not registered.");
    }
}
