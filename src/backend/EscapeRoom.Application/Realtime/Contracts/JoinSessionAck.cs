namespace EscapeRoom.Application.Realtime.Contracts;

public class JoinSessionAck
{
    public Guid SessionId { get; set; }
    public int CurrentVersion { get; set; }
    public int ReplayedDiffCount { get; set; }
    public int? LastKnownVersion { get; set; }
}
