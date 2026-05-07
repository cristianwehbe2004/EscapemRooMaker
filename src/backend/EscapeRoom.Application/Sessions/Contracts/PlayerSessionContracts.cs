namespace EscapeRoom.Application.Sessions.Contracts;

public class CreateSessionRequest
{
    public Guid? RoomId { get; set; }
    public int? DurationMinutes { get; set; }
    public string? DisplayName { get; set; }
}

public class JoinSessionRequest
{
    public string? DisplayName { get; set; }
    public string? GuestActorId { get; set; }
}

public class PlayerIdentity
{
    public string ActorId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Player";
    public bool IsAuthenticated { get; set; }
}

public class PlayerSessionSummary
{
    public Guid SessionId { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
    public int RemainingSeconds { get; set; }
    public bool IsQuickPlay { get; set; }
    public string PlayerJoinPath { get; set; } = string.Empty;
    public string GmJoinPath { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string JoinMode { get; set; } = "player";
    public bool CanSubmitActions { get; set; } = true;
}
