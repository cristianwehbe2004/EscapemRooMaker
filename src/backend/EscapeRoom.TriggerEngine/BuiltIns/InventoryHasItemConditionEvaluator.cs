using System.Text.Json.Nodes;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class InventoryHasItemConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(EvaluationContext context, TriggerNodeDefinition node)
    {
        var itemId = JsonStateHelpers.GetConfigString(node, "itemId");
        if (string.IsNullOrWhiteSpace(itemId) || context.State["inventory"] is not JsonArray inventory)
        {
            return false;
        }

        return inventory.Any(entry =>
            entry is JsonObject item &&
            string.Equals(item["id"]?.GetValue<string>(), itemId, StringComparison.OrdinalIgnoreCase));
    }
}
