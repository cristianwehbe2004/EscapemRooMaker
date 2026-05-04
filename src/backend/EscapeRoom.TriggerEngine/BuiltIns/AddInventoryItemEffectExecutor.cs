using System.Text.Json.Nodes;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class AddInventoryItemEffectExecutor : IEffectExecutor
{
    public EffectExecutionResult Execute(EvaluationContext context, TriggerNodeDefinition node)
    {
        var result = new EffectExecutionResult { Applied = true };
        var item = BuildItem(node);
        if (item is null)
        {
            return result;
        }

        var itemId = item["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return result;
        }

        var inventory = JsonStateHelpers.GetOrCreateArray(context.State, "inventory");
        var existing = inventory.FirstOrDefault(entry =>
            entry is JsonObject existingItem &&
            string.Equals(existingItem["id"]?.GetValue<string>(), itemId, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            inventory.Add(item);
        }

        result.ChangedEntities.Add("inventory");
        return result;
    }

    private static JsonObject? BuildItem(TriggerNodeDefinition node)
    {
        if (node.Config.TryGetValue("item", out var itemValue) && JsonStateHelpers.ToJsonNode(itemValue) is JsonObject item)
        {
            return item;
        }

        var id = JsonStateHelpers.GetConfigString(node, "id") ?? JsonStateHelpers.GetConfigString(node, "itemId");
        var label = JsonStateHelpers.GetConfigString(node, "label") ?? JsonStateHelpers.GetConfigString(node, "name") ?? id;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return new JsonObject
        {
            ["id"] = id,
            ["label"] = label,
            ["quantity"] = 1,
            ["type"] = JsonStateHelpers.GetConfigString(node, "type") ?? "generic",
            ["stack"] = false,
            ["status"] = "ready"
        };
    }
}
