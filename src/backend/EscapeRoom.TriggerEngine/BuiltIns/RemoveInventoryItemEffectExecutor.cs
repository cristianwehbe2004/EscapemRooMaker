using System.Text.Json.Nodes;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class RemoveInventoryItemEffectExecutor : IEffectExecutor
{
    public EffectExecutionResult Execute(EvaluationContext context, TriggerNodeDefinition node)
    {
        var result = new EffectExecutionResult { Applied = true };
        var itemId = JsonStateHelpers.GetConfigString(node, "itemId") ?? JsonStateHelpers.GetConfigString(node, "id");
        if (string.IsNullOrWhiteSpace(itemId) || context.State["inventory"] is not JsonArray inventory)
        {
            return result;
        }

        var match = inventory.FirstOrDefault(entry =>
            entry is JsonObject item &&
            string.Equals(item["id"]?.GetValue<string>(), itemId, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            inventory.Remove(match);
        }

        result.ChangedEntities.Add("inventory");
        return result;
    }
}
