namespace EscapeRoom.Application.Realtime.Contracts;

public class PlayerPresenceEvent
{
    public Guid SessionId { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "connected";
    public bool IsConnected { get; set; }
    public DateTime ConnectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}