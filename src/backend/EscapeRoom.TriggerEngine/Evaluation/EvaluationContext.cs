using System.Text.Json.Nodes;
using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.TriggerEngine.Evaluation;

public class EvaluationContext
{
    public Guid SessionId { get; init; }
    public Guid RoomId { get; init; }
    public PlayerActionEnvelope Action { get; init; } = new();
    public JsonObject State { get; init; } = new();
}
