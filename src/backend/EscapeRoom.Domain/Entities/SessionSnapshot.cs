using System;

namespace EscapeRoom.Domain.Entities;

public class SessionSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public GameSession Session { get; set; } = null!;
    public int Version { get; set; }
    public string StateData { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
