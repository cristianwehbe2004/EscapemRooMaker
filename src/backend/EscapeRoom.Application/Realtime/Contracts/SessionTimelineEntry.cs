namespace EscapeRoom.Application.Realtime.Contracts;

public class SessionTimelineEntry
{
    public Guid SessionId { get; set; }
    public int SequenceNumber { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}