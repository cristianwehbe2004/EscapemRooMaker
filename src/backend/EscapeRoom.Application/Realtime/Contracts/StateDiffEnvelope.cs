using System.Text.Json.Nodes;

namespace EscapeRoom.Application.Realtime.Contracts;

public class StateDiffEnvelope
{
    public int SessionVersion { get; set; }
    public long DiffSequence { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime EmittedAtUtc { get; set; } = DateTime.UtcNow;
    public List<string> ChangedEntities { get; set; } = new();
    public List<string> EmittedMessages { get; set; } = new();
    public List<string> AppliedEffects { get; set; } = new();
    public StatePatchEnvelope? StatePatch { get; set; }
    public string? FullStateJson { get; set; }
}

public class StatePatchEnvelope
{
    public JsonObject? Room { get; set; }
    public JsonArray? Inventory { get; set; }
    public List<string>? Messages { get; set; }
}
