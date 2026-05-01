using System.Text.Json;
using EscapeRoom.Application.Rooms.Contracts;
using EscapeRoom.Application.Triggering.Contracts;

namespace EscapeRoom.Infrastructure.Rooms;

internal static class EditorDocumentMapper
{
    public static EditorDocumentDto Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new EditorDocumentDto();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("triggerGraph", out _))
        {
            var parsed = JsonSerializer.Deserialize<EditorDocumentDto>(json, JsonOptions());
            return parsed ?? new EditorDocumentDto();
        }

        var graph = JsonSerializer.Deserialize<TriggerGraphDefinition>(json, JsonOptions()) ?? new TriggerGraphDefinition();
        return new EditorDocumentDto
        {
            Room = new VisualRoomDto(),
            TriggerGraph = graph
        };
    }

    public static string Serialize(EditorDocumentDto document)
        => JsonSerializer.Serialize(document, JsonOptions());

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);
}
