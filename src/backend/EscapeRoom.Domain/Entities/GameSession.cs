using EscapeRoom.Domain.Enums;

namespace EscapeRoom.Domain.Entities;

public class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public DateTime? EndsAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; } = DateTime.UtcNow;
    public string? HostActorId { get; set; }
    public bool IsQuickPlay { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Pending;
    public string StateSnapshot { get; set; } = "{}";
}
