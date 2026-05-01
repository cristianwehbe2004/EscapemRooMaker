namespace EscapeRoom.Application.Realtime.Contracts;

public class GmControlAction
{
    public string ControlType { get; set; } = string.Empty;
    public string? Target { get; set; }
    public Dictionary<string, object?> Payload { get; set; } = new();
    public string ClientActionId { get; set; } = Guid.NewGuid().ToString("N");
}