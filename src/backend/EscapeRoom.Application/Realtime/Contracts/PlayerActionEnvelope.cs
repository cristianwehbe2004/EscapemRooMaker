namespace EscapeRoom.Application.Realtime.Contracts;

public class PlayerActionEnvelope
{
    public string ActionType { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Target { get; set; }
    public Dictionary<string, object?> Payload { get; set; } = new();
    public string ClientActionId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
