using System.Text.Json.Nodes;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class SetObjectStateEffectExecutor : IEffectExecutor
{
    public EffectExecutionResult Execute(EvaluationContext context, TriggerNodeDefinition node)
    {
        var result = new EffectExecutionResult { Applied = true };
        var objectId = JsonStateHelpers.GetConfigString(node, "objectId") ?? JsonStateHelpers.GetConfigString(node, "id");
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return result;
        }

        var room = JsonStateHelpers.GetOrCreateObject(context.State, "room");
        var objectStates = JsonStateHelpers.GetOrCreateArray(room, "objectStates");
        var state = objectStates
            .OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(entry["id"]?.GetValue<string>(), objectId, StringComparison.OrdinalIgnoreCase));

        if (state is null)
        {
            state = new JsonObject
            {
                ["id"] = objectId,
                ["visible"] = true,
                ["available"] = true,
                ["locked"] = false,
                ["interactive"] = true
            };
            objectStates.Add(state);
        }

        ApplyBool(node, state, "visible");
        ApplyBool(node, state, "available");
        ApplyBool(node, state, "locked");
        ApplyBool(node, state, "interactive");

        result.ChangedEntities.Add($"object:{objectId}");
        result.ChangedEntities.Add("room");
        return result;
    }

    private static void ApplyBool(TriggerNodeDefinition node, JsonObject state, string key)
    {
        var value = JsonStateHelpers.GetConfigBool(node, key);
        if (value.HasValue)
        {
            state[key] = value.Value;
        }
    }
}
