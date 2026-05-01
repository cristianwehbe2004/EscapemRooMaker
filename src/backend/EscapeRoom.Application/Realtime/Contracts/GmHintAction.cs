namespace EscapeRoom.Application.Realtime.Contracts;

public class GmHintAction
{
    public string Hint { get; set; } = string.Empty;
    public string Scope { get; set; } = "session";
    public string? Target { get; set; }
    public string ClientActionId { get; set; } = Guid.NewGuid().ToString("N");
}
