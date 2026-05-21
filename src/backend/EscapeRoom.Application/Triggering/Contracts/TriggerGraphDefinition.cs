using System.Text.Json;
using System.Text.Json.Serialization;

namespace EscapeRoom.Application.Triggering.Contracts;

public class TriggerGraphDefinition
{
    public int Version { get; set; } = 1;

    [JsonConverter(typeof(MetadataDictionaryConverter))]
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

internal sealed class MetadataDictionaryConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            metadata[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Null => string.Empty,
                _ => property.Value.GetRawText()
            };
        }

        return metadata;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var entry in value)
        {
            writer.WriteString(entry.Key, entry.Value);
        }

        writer.WriteEndObject();
    }
}
