using System;

namespace EscapeRoom.Domain.Entities;

public class RoomVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string GraphDefinition { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
