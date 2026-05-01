using System.Text.Json;
using EscapeRoom.Application.Realtime;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Realtime;

public class GmPanelQueryService(AppDbContext dbContext) : IGmPanelQueryService
{
    public async Task<IReadOnlyList<GmSessionSummary>> GetActiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        return await (
                from session in dbContext.Sessions
                join room in dbContext.Rooms on session.RoomId equals room.Id
                orderby session.StartedAtUtc descending
                select new GmSessionSummary
                {
                    SessionId = session.Id,
                    RoomId = session.RoomId,
                    RoomName = room.Name,
                    Status = session.Status.ToString(),
                    StartedAtUtc = session.StartedAtUtc,
                    EndedAtUtc = session.EndedAtUtc,
                    ConnectedPlayers = 0
                })
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionTimelineEntry>> GetSessionTimelineAsync(Guid sessionId, int take = 100, CancellationToken cancellationToken = default)
    {
        var clampedTake = Math.Clamp(take, 1, 250);
        var events = await dbContext.SessionEvents
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.SequenceNumber)
            .Take(clampedTake)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return events
            .OrderBy(x => x.SequenceNumber)
            .Select(ToTimelineEntry)
            .ToList();
    }

    private static SessionTimelineEntry ToTimelineEntry(Domain.Entities.SessionEvent eventRecord)
    {
        PlayerActionEnvelope? action = null;
        try
        {
            action = JsonSerializer.Deserialize<PlayerActionEnvelope>(eventRecord.EventData, JsonOptions());
        }
        catch
        {
            // Ignore malformed JSON and fallback to event metadata.
        }

        var summary = BuildSummary(eventRecord.EventType, action);
        return new SessionTimelineEntry
        {
            SessionId = eventRecord.SessionId,
            SequenceNumber = eventRecord.SequenceNumber,
            EventType = eventRecord.EventType,
            Actor = action?.Actor ?? "system",
            Target = action?.Target,
            Summary = summary,
            OccurredAtUtc = eventRecord.OccurredAtUtc
        };
    }

    private static string BuildSummary(string eventType, PlayerActionEnvelope? action)
    {
        var normalized = eventType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "gm.hint" => Prefix("Hint", GetPayloadString(action, "hint")),
            "gm.broadcast" => Prefix("Broadcast", GetPayloadString(action, "message")),
            "gm.reveal" => $"Reveal requested for {GetPayloadString(action, "puzzleId") ?? action?.Target ?? "unknown target"}",
            "gm.force_sync" => "Force sync requested",
            _ => $"{eventType} by {action?.Actor ?? "system"}"
        };
    }

    private static string Prefix(string prefix, string? value)
        => string.IsNullOrWhiteSpace(value) ? prefix : $"{prefix}: {value}";

    private static string? GetPayloadString(PlayerActionEnvelope? action, string key)
    {
        if (action?.Payload is null || !action.Payload.TryGetValue(key, out var raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            string s => s,
            JsonElement j when j.ValueKind == JsonValueKind.String => j.GetString(),
            JsonElement j => j.ToString(),
            _ => raw.ToString()
        };
    }

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);
}