using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.Realtime.Presence;

public class InMemoryPlayerPresenceTracker : IPlayerPresenceTracker
{
    private readonly object sync = new();
    private readonly Dictionary<string, PresenceRecord> byConnectionId = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Dictionary<string, PresenceRecord>> bySessionId = new();

    public PlayerPresenceEvent TrackConnected(Guid sessionId, string playerId, string displayName, string connectionId)
    {
        var now = DateTime.UtcNow;
        lock (sync)
        {
            if (byConnectionId.TryGetValue(connectionId, out var existing))
            {
                RemoveInternal(existing);
            }

            var record = new PresenceRecord
            {
                SessionId = sessionId,
                PlayerId = playerId,
                DisplayName = displayName,
                ConnectionId = connectionId,
                ConnectedAtUtc = now,
                LastSeenUtc = now
            };

            if (!bySessionId.TryGetValue(sessionId, out var sessionPlayers))
            {
                sessionPlayers = new Dictionary<string, PresenceRecord>(StringComparer.Ordinal);
                bySessionId[sessionId] = sessionPlayers;
            }

            sessionPlayers[connectionId] = record;
            byConnectionId[connectionId] = record;

            return ToEvent(record, "connected", isConnected: true, lastSeenUtc: now);
        }
    }

    public PlayerPresenceEvent? TrackDisconnected(string connectionId)
    {
        var now = DateTime.UtcNow;
        lock (sync)
        {
            if (!byConnectionId.TryGetValue(connectionId, out var record))
            {
                return null;
            }

            RemoveInternal(record);
            return ToEvent(record, "disconnected", isConnected: false, lastSeenUtc: now);
        }
    }

    public IReadOnlyList<PlayerPresenceEvent> GetSessionPresence(Guid sessionId)
    {
        lock (sync)
        {
            if (!bySessionId.TryGetValue(sessionId, out var sessionPlayers))
            {
                return [];
            }

            return sessionPlayers.Values
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(x => ToEvent(x, "connected", isConnected: true, x.LastSeenUtc))
                .ToList();
        }
    }

    public int GetConnectedCount(Guid sessionId)
    {
        lock (sync)
        {
            return bySessionId.TryGetValue(sessionId, out var sessionPlayers)
                ? sessionPlayers.Count
                : 0;
        }
    }

    private void RemoveInternal(PresenceRecord record)
    {
        byConnectionId.Remove(record.ConnectionId);
        if (bySessionId.TryGetValue(record.SessionId, out var sessionPlayers))
        {
            sessionPlayers.Remove(record.ConnectionId);
            if (sessionPlayers.Count == 0)
            {
                bySessionId.Remove(record.SessionId);
            }
        }
    }

    private static PlayerPresenceEvent ToEvent(PresenceRecord record, string status, bool isConnected, DateTime lastSeenUtc)
        => new()
        {
            SessionId = record.SessionId,
            PlayerId = record.PlayerId,
            DisplayName = record.DisplayName,
            Status = status,
            IsConnected = isConnected,
            ConnectedAtUtc = record.ConnectedAtUtc,
            LastSeenUtc = lastSeenUtc
        };

    private sealed class PresenceRecord
    {
        public Guid SessionId { get; set; }
        public string PlayerId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public DateTime ConnectedAtUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}