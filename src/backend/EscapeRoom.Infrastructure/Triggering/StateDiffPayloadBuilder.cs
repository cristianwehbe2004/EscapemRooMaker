using System.Text.Json.Nodes;
using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.Infrastructure.Triggering;

public static class StateDiffPayloadBuilder
{
    private static readonly string[] MessageOnlyPrefixes =
    [
        "message",
        "messages",
        "gm.hint",
        "gm.broadcast",
        "ui.message",
        "chat"
    ];

    private static readonly string[] CluePrefixes =
    [
        "clue",
        "clues"
    ];

    private static readonly string[] InventoryPrefixes =
    [
        "inventory",
        "item"
    ];

    private static readonly string[] RoomPrefixes =
    [
        "room",
        "state",
        "hotspot",
        "object",
        "interactable",
        "asset",
        "layer",
        "puzzle"
    ];

    private static readonly string[] SessionPrefixes =
    [
        "session",
        "timer"
    ];

    public static (StatePatchEnvelope? StatePatch, string? FullStateJson) Build(
        string updatedStateJson,
        string actionType,
        IReadOnlyCollection<string> changedEntities)
    {
        if (string.IsNullOrWhiteSpace(updatedStateJson))
        {
            return (null, null);
        }

        var normalizedChanges = changedEntities
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var hasNonMessageMutations = normalizedChanges.Any(change => !HasPrefix(change, MessageOnlyPrefixes));
        var includesInventory = IsInventoryAction(actionType) || normalizedChanges.Any(change => HasPrefix(change, InventoryPrefixes));
        var includesRoom = IsRoomAction(actionType) || normalizedChanges.Any(change => HasPrefix(change, RoomPrefixes));
        var includesClues = normalizedChanges.Any(change => HasPrefix(change, CluePrefixes));
        var includesSession = normalizedChanges.Any(change => HasPrefix(change, SessionPrefixes));

        if (!includesInventory && !includesRoom && !includesClues && !includesSession)
        {
            return hasNonMessageMutations ? (null, updatedStateJson) : (null, null);
        }

        if (JsonNode.Parse(updatedStateJson) is not JsonObject stateRoot)
        {
            return (null, updatedStateJson);
        }

        var statePatch = new StatePatchEnvelope();
        if (includesInventory && stateRoot["inventory"] is JsonArray inventory)
        {
            statePatch.Inventory = (JsonArray)inventory.DeepClone();
        }

        if (includesClues && stateRoot["clues"] is JsonArray clues)
        {
            statePatch.Clues = (JsonArray)clues.DeepClone();
        }

        if (includesSession && stateRoot["session"] is JsonObject session)
        {
            statePatch.Session = (JsonObject)session.DeepClone();
        }

        if (includesRoom && stateRoot["room"] is JsonObject room)
        {
            var roomPatch = new JsonObject();
            CopyNode(room, roomPatch, "roomName");
            CopyNode(room, roomPatch, "themeId");
            CopyNode(room, roomPatch, "width");
            CopyNode(room, roomPatch, "height");
            CopyNode(room, roomPatch, "backgroundColor");
            CopyNode(room, roomPatch, "assets");
            CopyNode(room, roomPatch, "layers");
            CopyNode(room, roomPatch, "hotspots");
            CopyNode(room, roomPatch, "objectStates");
            CopyNode(room, roomPatch, "interactables");

            if (normalizedChanges.Any(change => string.Equals(change, "room.transition", StringComparison.Ordinal)))
            {
                roomPatch["replace"] = true;
            }

            if (roomPatch.Count > 0)
            {
                statePatch.Room = roomPatch;
            }
        }

        if (statePatch.Inventory is null && statePatch.Room is null && statePatch.Clues is null && statePatch.Session is null)
        {
            return hasNonMessageMutations ? (null, updatedStateJson) : (null, null);
        }

        return (statePatch, null);
    }

    private static bool IsInventoryAction(string actionType)
        => actionType.Trim().StartsWith("inventory.", StringComparison.OrdinalIgnoreCase);

    private static bool IsRoomAction(string actionType)
    {
        var normalized = actionType.Trim().ToLowerInvariant();
        return normalized is "inspect" or "pickup"
            || normalized.StartsWith("room.", StringComparison.Ordinal)
            || normalized.StartsWith("gm.", StringComparison.Ordinal);
    }

    private static bool HasPrefix(string value, IEnumerable<string> prefixes)
        => prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));

    private static void CopyNode(JsonObject source, JsonObject destination, string key)
    {
        if (source[key] is JsonNode node)
        {
            destination[key] = node.DeepClone();
        }
    }
}
