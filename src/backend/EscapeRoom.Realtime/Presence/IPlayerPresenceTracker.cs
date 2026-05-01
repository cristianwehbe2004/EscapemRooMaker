using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.Realtime.Presence;

public interface IPlayerPresenceTracker
{
    PlayerPresenceEvent TrackConnected(Guid sessionId, string playerId, string displayName, string connectionId);
    PlayerPresenceEvent? TrackDisconnected(string connectionId);
    IReadOnlyList<PlayerPresenceEvent> GetSessionPresence(Guid sessionId);
    int GetConnectedCount(Guid sessionId);
}