namespace EscapeRoom.Application.Realtime.Contracts;

public class GmSessionSummary
{
    public Guid SessionId { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int ConnectedPlayers { get; set; }
}