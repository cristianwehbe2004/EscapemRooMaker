using System.Text.Json;
using System.Text.Json.Nodes;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Domain.Enums;
using EscapeRoom.Infrastructure.Rooms;

namespace EscapeRoom.Infrastructure.Sessions;

public static class SessionStateFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string BuildInitialState(Room room, GameSession session, DateTime serverTimeUtc)
    {
        var document = EditorDocumentMapper.Deserialize(room.GraphDefinition);
        return JsonSerializer.Serialize(new
        {
            room = document.Room,
            inventory = Array.Empty<object>(),
            clues = Array.Empty<string>(),
            messages = new[] { session.Status == SessionStatus.Active ? "Session started." : "Session created. Waiting to start." },
            session = BuildSessionState(room, session, serverTimeUtc)
        }, JsonOptions);
    }

    public static string WithSessionState(string stateSnapshot, Room room, GameSession session, DateTime serverTimeUtc)
    {
        var state = JsonNode.Parse(string.IsNullOrWhiteSpace(stateSnapshot) ? "{}" : stateSnapshot) as JsonObject ?? new JsonObject();
        var nextSession = JsonSerializer.SerializeToNode(BuildSessionState(room, session, serverTimeUtc), JsonOptions) as JsonObject
            ?? new JsonObject();
        if (state["session"] is JsonObject currentSession)
        {
            foreach (var property in currentSession)
            {
                if (!nextSession.ContainsKey(property.Key))
                {
                    nextSession[property.Key] = property.Value?.DeepClone();
                }
            }
        }

        state["session"] = nextSession;
        return state.ToJsonString(JsonOptions);
    }

    public static object BuildSessionState(Room room, GameSession session, DateTime serverTimeUtc)
        => new
        {
            sessionId = session.Id,
            roomId = room.Id,
            roomName = room.Name,
            status = session.Status.ToString(),
            durationMinutes = session.DurationMinutes,
            startedAtUtc = session.StartedAtUtc,
            endedAtUtc = session.EndedAtUtc,
            endsAtUtc = session.EndsAtUtc,
            serverTimeUtc,
            remainingSeconds = ResolveRemainingSeconds(session, serverTimeUtc),
            isQuickPlay = session.IsQuickPlay
        };

    public static int ResolveRemainingSeconds(GameSession session, DateTime serverTimeUtc)
    {
        if (session.Status == SessionStatus.Completed || session.Status == SessionStatus.Cancelled || session.Status == SessionStatus.Expired)
        {
            return 0;
        }

        if (session.Status != SessionStatus.Active || session.EndsAtUtc is null)
        {
            return session.DurationMinutes * 60;
        }

        return Math.Max(0, (int)Math.Ceiling((session.EndsAtUtc.Value - serverTimeUtc).TotalSeconds));
    }
}
