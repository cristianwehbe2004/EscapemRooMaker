namespace EscapeRoom.Application.Realtime.Contracts;

public class RecoverSessionResult
{
    public Guid SessionId { get; set; }
    public int CurrentVersion { get; set; }
    public int ReplayedDiffCount { get; set; }
    public bool SnapshotSent { get; set; }
}
