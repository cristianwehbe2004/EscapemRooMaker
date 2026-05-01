namespace EscapeRoom.Realtime.RateLimiting;

public class ActionRateLimitOptions
{
    public const string SectionName = "ActionRateLimit";

    public int CooldownMs { get; set; } = 900;
    public string PolicyName { get; set; } = "player-action-default";
}
