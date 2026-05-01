using System.Text.Json.Serialization;

namespace EscapeRoom.Application.Triggering.Contracts;

public class TriggerGraphDefinition
{
    public int Version { get; set; } = 1;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<TriggerNodeDefinition> Nodes { get; set; } = new();
    public List<TriggerEdgeDefinition> Edges { get; set; } = new();
}

public class TriggerNodeDefinition
{
    public string NodeId { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object?> Config { get; set; } = new();
    public EffectPolicyDefinition Policy { get; set; } = new();
}

public class TriggerEdgeDefinition
{
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
}

public class EffectPolicyDefinition
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "one-shot";

    [JsonPropertyName("keyWindowSeconds")]
    public int? KeyWindowSeconds { get; set; }
}
