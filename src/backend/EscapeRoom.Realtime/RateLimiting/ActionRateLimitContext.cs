namespace EscapeRoom.Realtime.RateLimiting;

public class ActionRateLimitContext
{
    public string PolicyScope { get; set; } = "player";
    public string ActorRole { get; set; } = "player";
}

