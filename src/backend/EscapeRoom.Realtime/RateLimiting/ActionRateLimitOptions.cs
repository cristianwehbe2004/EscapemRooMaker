namespace EscapeRoom.Realtime.RateLimiting;

public class ActionRateLimitOptions
{
    public const string SectionName = "ActionRateLimit";

    // Backward-compatible defaults for player policy.
    public int CooldownMs { get; set; } = 900;
    public string PolicyName { get; set; } = "player-action-default";

    public ActionRateLimitPolicyOptions Player { get; set; } = new() { Enabled = true };

    public ActionRateLimitPolicyOptions Gm { get; set; } = new()
    {
        Enabled = false,
        CooldownMs = 0,
        PolicyName = "gm-action-bypass"
    };
}

public class ActionRateLimitPolicyOptions
{
    public bool Enabled { get; set; } = true;
    public int CooldownMs { get; set; }
    public string PolicyName { get; set; } = string.Empty;
}
