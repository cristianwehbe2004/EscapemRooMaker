namespace EscapeRoom.Domain.Entities;

public class RoomRating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public int Score { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
