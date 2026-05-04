using System.Text.Json;
using System.Text.Json.Nodes;
using EscapeRoom.Application.Triggering.Contracts;

namespace EscapeRoom.TriggerEngine.BuiltIns;

internal static class JsonStateHelpers
{
    public static string? GetConfigString(TriggerNodeDefinition node, string key)
    {
        if (!node.Config.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element => element.ToString(),
            _ => value.ToString()
        };
    }

    public static bool? GetConfigBool(TriggerNodeDefinition node, string key)
    {
        if (!node.Config.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            bool flag => flag,
            JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            _ when bool.TryParse(value.ToString(), out var parsed) => parsed,
            _ => null
        };
    }

    public static JsonNode? ToJsonNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonNode node)
        {
            return node.DeepClone();
        }

        if (value is JsonElement element)
        {
            return JsonNode.Parse(element.GetRawText());
        }

        return JsonSerializer.SerializeToNode(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public static JsonObject GetOrCreateObject(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[key] = created;
        return created;
    }

    public static JsonArray GetOrCreateArray(JsonObject parent, string key)
    {
        if (parent[key] is JsonArray existing)
        {
            return existing;
        }

        var created = new JsonArray();
        parent[key] = created;
        return created;
    }

    public static JsonNode? ResolvePath(JsonObject root, string path)
    {
        JsonNode? current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is not JsonObject currentObject)
            {
                return null;
            }

            current = currentObject[segment];
        }

        return current;
    }

    public static void SetPath(JsonObject root, string path, JsonNode? value)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return;
        }

        var current = root;
        foreach (var segment in segments.Take(segments.Length - 1))
        {
            current = GetOrCreateObject(current, segment);
        }

        current[segments[^1]] = value;
    }
}
