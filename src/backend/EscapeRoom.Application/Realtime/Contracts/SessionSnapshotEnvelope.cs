namespace EscapeRoom.Application.Realtime.Contracts;

public class SessionSnapshotEnvelope
{
    public Guid SessionId { get; set; }
    public int SessionVersion { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
    public List<PlayerPresenceEvent> PlayerPresence { get; set; } = new();
}
