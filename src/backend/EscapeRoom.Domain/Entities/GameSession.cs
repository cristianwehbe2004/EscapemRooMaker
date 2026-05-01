using EscapeRoom.Domain.Enums;

namespace EscapeRoom.Domain.Entities;

public class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Pending;
    public string StateSnapshot { get; set; } = "{}";
}
