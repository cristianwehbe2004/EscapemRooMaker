using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.BuiltIns;

namespace EscapeRoom.TriggerEngine.Registry;

public class ConditionRegistry(
    ActionTypeConditionEvaluator actionTypeConditionEvaluator,
    TargetEqualsConditionEvaluator targetEqualsConditionEvaluator,
    InventoryHasItemConditionEvaluator inventoryHasItemConditionEvaluator,
    StateValueEqualsConditionEvaluator stateValueEqualsConditionEvaluator) : IConditionRegistry
{
    private readonly Dictionary<string, IConditionEvaluator> _evaluators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["actionTypeEquals"] = actionTypeConditionEvaluator,
        ["targetEquals"] = targetEqualsConditionEvaluator,
        ["inventoryHasItem"] = inventoryHasItemConditionEvaluator,
        ["stateValueEquals"] = stateValueEqualsConditionEvaluator
    };

    public IConditionEvaluator Get(string type)
    {
        if (_evaluators.TryGetValue(type, out var evaluator))
        {
            return evaluator;
        }

        throw new InvalidOperationException($"Condition evaluator '{type}' is not registered.");
    }
}
