using System.Text.Json.Nodes;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.Evaluation;

namespace EscapeRoom.TriggerEngine.BuiltIns;

public class TransitionRoomEffectExecutor : IEffectExecutor
{
    public EffectExecutionResult Execute(EvaluationContext context, TriggerNodeDefinition node)
    {
        var result = new EffectExecutionResult { Applied = true };
        if (!node.Config.TryGetValue("room", out var roomValue))
        {
            return result;
        }

        if (JsonStateHelpers.ToJsonNode(roomValue) is not JsonObject roomNode)
        {
            return result;
        }

        context.State["room"] = roomNode.DeepClone();

        var session = JsonStateHelpers.GetOrCreateObject(context.State, "session");
        if (roomNode["roomName"] is JsonValue roomNameValue && roomNameValue.TryGetValue<string>(out var roomName) && !string.IsNullOrWhiteSpace(roomName))
        {
            session["roomName"] = roomName;
        }

        var message = JsonStateHelpers.GetConfigString(node, "message");
        if (!string.IsNullOrWhiteSpace(message))
        {
            result.Messages.Add(message);
        }

        result.ChangedEntities.Add("room.transition");
        result.ChangedEntities.Add("room");
        result.ChangedEntities.Add("session");
        return result;
    }
}
