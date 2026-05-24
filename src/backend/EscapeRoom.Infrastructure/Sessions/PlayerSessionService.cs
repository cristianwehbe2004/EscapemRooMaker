using System.Text.Json;
using System.Text.Json.Nodes;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Sessions;
using EscapeRoom.Application.Sessions.Contracts;
using EscapeRoom.Application.Triggering;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Domain.Enums;
using EscapeRoom.Infrastructure.Data;
using EscapeRoom.Infrastructure.Rooms;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Sessions;

public class PlayerSessionService(
    AppDbContext dbContext,
    ISessionStateStore sessionStateStore) : IPlayerSessionService
{
    private const int ForcedDurationMinutes = 10;
    private const string ClocktowerRoomName = "Clocktower Foyer";
    private const int ClocktowerDurationMinutes = 3;
    private const string JoinModePlayer = "player";

    public async Task<PlayerSessionSummary> CreateSessionAsync(
        CreateSessionRequest request,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var room = await ResolvePublishedRoomAsync(request.RoomId, cancellationToken);
        var now = DateTime.UtcNow;
        var durationMinutes = ResolveDurationMinutes(room);
        var session = new GameSession
        {
            RoomId = room.Id,
            Status = SessionStatus.Pending,
            StartedAtUtc = now,
            LastActivityAtUtc = now,
            DurationMinutes = durationMinutes,
            HostActorId = identity.ActorId,
            IsQuickPlay = false
        };
        session.StateSnapshot = SessionStateFactory.BuildInitialState(room, session, now);
        UpsertParticipantInState(session, room, identity, now, new JoinAccess(JoinModePlayer, true));

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveRealtimeSnapshotAsync(session, now, cancellationToken);
        return BuildSummary(room, session, identity, now, new JoinAccess(JoinModePlayer, true));
    }

    public async Task<PlayerSessionSummary> QuickStartAsync(
        CreateSessionRequest request,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var room = await ResolvePublishedRoomAsync(request.RoomId, cancellationToken);
        var now = DateTime.UtcNow;
        var durationMinutes = ResolveDurationMinutes(room);
        var session = new GameSession
        {
            RoomId = room.Id,
            Status = SessionStatus.Active,
            StartedAtUtc = now,
            LastActivityAtUtc = now,
            DurationMinutes = durationMinutes,
            EndsAtUtc = now.AddMinutes(durationMinutes),
            HostActorId = identity.ActorId,
            IsQuickPlay = true
        };
        session.StateSnapshot = SessionStateFactory.BuildInitialState(room, session, now);
        UpsertParticipantInState(session, room, identity, now, new JoinAccess(JoinModePlayer, true));

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveRealtimeSnapshotAsync(session, now, cancellationToken);
        return BuildSummary(room, session, identity, now, new JoinAccess(JoinModePlayer, true));
    }

    public async Task<PlayerSessionSummary> JoinSessionAsync(
        Guid sessionId,
        JoinSessionRequest request,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var (session, room) = await GetSessionWithRoomAsync(sessionId, cancellationToken);
        await RefreshExpirationAsync(session, room, cancellationToken);

        var now = DateTime.UtcNow;
        var access = ResolveJoinAccess(session, identity.ActorId);
        var changed = UpsertParticipantInState(session, room, identity, now, access);
        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await SaveRealtimeSnapshotAsync(session, now, cancellationToken);
        }

        return BuildSummary(room, session, identity, now, access);
    }

    public async Task<PlayerSessionSummary> StartSessionAsync(
        Guid sessionId,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var (session, room) = await GetSessionWithRoomAsync(sessionId, cancellationToken);
        if (session.Status == SessionStatus.Active)
        {
            var activeAccess = ResolveJoinAccess(session, identity.ActorId);
            return BuildSummary(room, session, identity, DateTime.UtcNow, activeAccess);
        }

        if (session.Status != SessionStatus.Pending)
        {
            throw new SessionServiceException($"Session '{sessionId}' cannot be started from status '{session.Status}'.");
        }

        if (!string.IsNullOrWhiteSpace(session.HostActorId) &&
            !session.HostActorId.Equals(identity.ActorId, StringComparison.OrdinalIgnoreCase) &&
            identity.IsAuthenticated)
        {
            throw new SessionAccessDeniedException("Only the session host can start this session.");
        }

        var now = DateTime.UtcNow;
        session.Status = SessionStatus.Active;
        session.StartedAtUtc = now;
        session.LastActivityAtUtc = now;
        session.EndsAtUtc = now.AddMinutes(session.DurationMinutes);
        session.StateSnapshot = SessionStateFactory.WithSessionState(session.StateSnapshot, room, session, now);
        UpsertParticipantInState(session, room, identity, now, new JoinAccess(JoinModePlayer, true));

        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveRealtimeSnapshotAsync(session, now, cancellationToken);
        return BuildSummary(room, session, identity, now, new JoinAccess(JoinModePlayer, true));
    }

    public async Task<PlayerSessionSummary> GetSessionAsync(
        Guid sessionId,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var (session, room) = await GetSessionWithRoomAsync(sessionId, cancellationToken);
        await RefreshExpirationAsync(session, room, cancellationToken);
        var access = ResolveJoinAccess(session, identity.ActorId);
        return BuildSummary(room, session, identity, DateTime.UtcNow, access);
    }

    public async Task<PlayerSessionSummary> KickParticipantAsync(
        Guid sessionId,
        string targetActorId,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var (session, room) = await GetSessionWithRoomAsync(sessionId, cancellationToken);
        await RefreshExpirationAsync(session, room, cancellationToken);

        if (session.Status != SessionStatus.Pending)
        {
            throw new SessionServiceException("Participants can only be kicked before the session starts.");
        }

        if (string.IsNullOrWhiteSpace(session.HostActorId) ||
            !session.HostActorId.Equals(identity.ActorId, StringComparison.OrdinalIgnoreCase))
        {
            throw new SessionAccessDeniedException("Only the session host can remove participants.");
        }

        if (string.IsNullOrWhiteSpace(targetActorId))
        {
            throw new SessionServiceException("A target actor id is required.");
        }

        if (session.HostActorId.Equals(targetActorId, StringComparison.OrdinalIgnoreCase))
        {
            throw new SessionServiceException("The host cannot remove themselves from the session.");
        }

        var now = DateTime.UtcNow;
        var changed = RemoveParticipantFromState(session, room, targetActorId, now);
        if (!changed)
        {
            throw new SessionServiceException("Participant was not found in this session.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveRealtimeSnapshotAsync(session, now, cancellationToken);
        var access = ResolveJoinAccess(session, identity.ActorId);
        return BuildSummary(room, session, identity, now, access);
    }

    public async Task<bool> CanSubmitActionsAsync(
        Guid sessionId,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return false;
        }

        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new SessionNotFoundException(sessionId);

        if (string.Equals(session.HostActorId, actorId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var participant = FindParticipantAccess(session.StateSnapshot, actorId);
        if (participant is not null)
        {
            return participant.Value.CanSubmitActions;
        }

        return session.Status != SessionStatus.Active;
    }

    private static int ResolveDurationMinutes(Room room)
    {
        if (string.Equals(room.Name, ClocktowerRoomName, StringComparison.OrdinalIgnoreCase))
        {
            return ClocktowerDurationMinutes;
        }

        try
        {
            var document = EditorDocumentMapper.Deserialize(room.GraphDefinition);
            if (document.TriggerGraph.Metadata.TryGetValue("estimatedMinutes", out var estimatedValue) &&
                int.TryParse(estimatedValue, out var estimatedMinutes) &&
                estimatedMinutes > 0)
            {
                return estimatedMinutes;
            }
        }
        catch
        {
            // Fall back to the existing default when room metadata is unavailable.
        }

        return ForcedDurationMinutes;
    }

    private async Task<Room> ResolvePublishedRoomAsync(Guid? roomId, CancellationToken cancellationToken)
    {
        var query = dbContext.Rooms.Where(x => x.IsPublished);
        if (roomId.HasValue)
        {
            var selected = await query.FirstOrDefaultAsync(x => x.Id == roomId.Value, cancellationToken);
            return selected ?? throw new PublishedRoomNotFoundException(roomId.Value);
        }

        var room = await query.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return room ?? throw new NoPublishedRoomAvailableException();
    }

    private async Task<(GameSession Session, Room Room)> GetSessionWithRoomAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new SessionNotFoundException(sessionId);
        var room = await dbContext.Rooms.FirstOrDefaultAsync(x => x.Id == session.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException(session.RoomId);
        return (session, room);
    }

    private async Task RefreshExpirationAsync(GameSession session, Room room, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (session.Status != SessionStatus.Active || session.EndsAtUtc is null || session.EndsAtUtc > now)
        {
            return;
        }

        session.Status = SessionStatus.Expired;
        session.EndedAtUtc = now;
        session.LastActivityAtUtc = now;
        session.StateSnapshot = SessionStateFactory.WithSessionState(session.StateSnapshot, room, session, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveRealtimeSnapshotAsync(session, now, cancellationToken);
    }

    private async Task SaveRealtimeSnapshotAsync(GameSession session, DateTime now, CancellationToken cancellationToken)
    {
        await sessionStateStore.SaveSnapshotAsync(new SessionSnapshotEnvelope
        {
            SessionId = session.Id,
            SessionVersion = 0,
            StateJson = session.StateSnapshot,
            ServerTimeUtc = now
        }, cancellationToken);
    }

    private static PlayerSessionSummary BuildSummary(Room room, GameSession session, PlayerIdentity identity, DateTime now, JoinAccess access)
    {
        var participants = EnsureHostParticipant(ExtractParticipants(session), session, identity, now, access);
        return new PlayerSessionSummary
        {
            SessionId = session.Id,
            RoomId = room.Id,
            RoomName = room.Name,
            Status = session.Status.ToString(),
            DurationMinutes = session.DurationMinutes,
            StartedAtUtc = session.StartedAtUtc,
            EndedAtUtc = session.EndedAtUtc,
            EndsAtUtc = session.EndsAtUtc,
            ServerTimeUtc = now,
            RemainingSeconds = SessionStateFactory.ResolveRemainingSeconds(session, now),
            IsQuickPlay = session.IsQuickPlay,
            PlayerJoinPath = $"/player?sessionId={session.Id}",
            GmJoinPath = $"/gm?sessionId={session.Id}",
            ActorId = identity.ActorId,
            DisplayName = identity.DisplayName,
            IsHost = string.Equals(session.HostActorId, identity.ActorId, StringComparison.OrdinalIgnoreCase),
            JoinMode = access.JoinMode,
            CanSubmitActions = access.CanSubmitActions,
            Participants = participants
        };
    }

    private static JoinAccess ResolveJoinAccess(GameSession session, string actorId)
    {
        if (string.Equals(session.HostActorId, actorId, StringComparison.OrdinalIgnoreCase))
        {
            return new JoinAccess(JoinModePlayer, true);
        }

        var participant = FindParticipantAccess(session.StateSnapshot, actorId);
        if (participant is not null)
        {
            return participant.Value;
        }

        return new JoinAccess(JoinModePlayer, true);
    }

    private static bool UpsertParticipantInState(
        GameSession session,
        Room room,
        PlayerIdentity identity,
        DateTime now,
        JoinAccess access)
    {
        var state = ParseState(session.StateSnapshot);
        var sessionNode = state["session"] as JsonObject ?? new JsonObject();
        state["session"] = sessionNode;

        var participants = sessionNode["participants"] as JsonArray ?? new JsonArray();
        sessionNode["participants"] = participants;

        var participant = participants
            .OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(entry["actorId"]?.GetValue<string>(), identity.ActorId, StringComparison.OrdinalIgnoreCase));

        var changed = false;
        if (participant is null)
        {
            participant = new JsonObject();
            participants.Add(participant);
            changed = true;
        }

        changed |= SetString(participant, "actorId", identity.ActorId);
        changed |= SetString(participant, "displayName", identity.DisplayName);
        changed |= SetString(participant, "joinMode", access.JoinMode);
        changed |= SetBool(participant, "canSubmitActions", access.CanSubmitActions);
        changed |= SetString(participant, "lastSeenAtUtc", now.ToString("O"));
        if (participant["joinedAtUtc"] is null)
        {
            participant["joinedAtUtc"] = now.ToString("O");
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        session.StateSnapshot = state.ToJsonString(JsonOptions());
        session.StateSnapshot = SessionStateFactory.WithSessionState(session.StateSnapshot, room, session, now);
        return true;
    }

    private static JoinAccess? FindParticipantAccess(string stateSnapshot, string actorId)
    {
        var state = ParseState(stateSnapshot);
        if (state["session"] is not JsonObject sessionNode || sessionNode["participants"] is not JsonArray participants)
        {
            return null;
        }

        var participant = participants
            .OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(entry["actorId"]?.GetValue<string>(), actorId, StringComparison.OrdinalIgnoreCase));

        if (participant is null)
        {
            return null;
        }

        var joinMode = participant["joinMode"]?.GetValue<string>() ?? JoinModePlayer;
        var canSubmitActions = participant["canSubmitActions"]?.GetValue<bool>() ?? true;
        return new JoinAccess(joinMode, canSubmitActions);
    }

    private static bool RemoveParticipantFromState(
        GameSession session,
        Room room,
        string actorId,
        DateTime now)
    {
        var state = ParseState(session.StateSnapshot);
        if (state["session"] is not JsonObject sessionNode || sessionNode["participants"] is not JsonArray participants)
        {
            return false;
        }

        var removed = false;
        for (var i = participants.Count - 1; i >= 0; i--)
        {
            if (participants[i] is not JsonObject participant)
            {
                continue;
            }

            var participantActorId = participant["actorId"]?.GetValue<string>();
            if (!string.Equals(participantActorId, actorId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            participants.RemoveAt(i);
            removed = true;
        }

        if (!removed)
        {
            return false;
        }

        session.StateSnapshot = state.ToJsonString(JsonOptions());
        session.StateSnapshot = SessionStateFactory.WithSessionState(session.StateSnapshot, room, session, now);
        return true;
    }

    private static IReadOnlyList<PlayerSessionParticipant> ExtractParticipants(GameSession session)
    {
        var state = ParseState(session.StateSnapshot);
        if (state["session"] is not JsonObject sessionNode || sessionNode["participants"] is not JsonArray participants)
        {
            return [];
        }

        var result = new List<PlayerSessionParticipant>();
        foreach (var participantNode in participants.OfType<JsonObject>())
        {
            var actorId = participantNode["actorId"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(actorId))
            {
                continue;
            }

            DateTime? joinedAtUtc = null;
            if (DateTime.TryParse(participantNode["joinedAtUtc"]?.GetValue<string>(), out var joinedAt))
            {
                joinedAtUtc = joinedAt;
            }

            DateTime? lastSeenAtUtc = null;
            if (DateTime.TryParse(participantNode["lastSeenAtUtc"]?.GetValue<string>(), out var lastSeenAt))
            {
                lastSeenAtUtc = lastSeenAt;
            }

            result.Add(new PlayerSessionParticipant
            {
                ActorId = actorId,
                DisplayName = participantNode["displayName"]?.GetValue<string>() ?? "Player",
                JoinMode = participantNode["joinMode"]?.GetValue<string>() ?? JoinModePlayer,
                CanSubmitActions = participantNode["canSubmitActions"]?.GetValue<bool>() ?? true,
                IsHost = string.Equals(session.HostActorId, actorId, StringComparison.OrdinalIgnoreCase),
                JoinedAtUtc = joinedAtUtc,
                LastSeenAtUtc = lastSeenAtUtc
            });
        }

        return result
            .OrderByDescending(x => x.IsHost)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<PlayerSessionParticipant> EnsureHostParticipant(
        IReadOnlyList<PlayerSessionParticipant> participants,
        GameSession session,
        PlayerIdentity identity,
        DateTime now,
        JoinAccess access)
    {
        if (participants.Count > 0)
        {
            return participants;
        }

        var actorId = !string.IsNullOrWhiteSpace(session.HostActorId) ? session.HostActorId : identity.ActorId;
        var displayName = !string.IsNullOrWhiteSpace(identity.DisplayName) ? identity.DisplayName : "Player";
        return
        [
            new PlayerSessionParticipant
            {
                ActorId = actorId,
                DisplayName = displayName,
                JoinMode = access.JoinMode,
                CanSubmitActions = access.CanSubmitActions,
                IsHost = true,
                JoinedAtUtc = now,
                LastSeenAtUtc = now
            }
        ];
    }

    private static JsonObject ParseState(string json)
        => JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? new JsonObject();

    private static bool SetString(JsonObject target, string key, string value)
    {
        if (string.Equals(target[key]?.GetValue<string>(), value, StringComparison.Ordinal))
        {
            return false;
        }

        target[key] = value;
        return true;
    }

    private static bool SetBool(JsonObject target, string key, bool value)
    {
        if (target[key]?.GetValue<bool>() == value)
        {
            return false;
        }

        target[key] = value;
        return true;
    }

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);

    private readonly record struct JoinAccess(string JoinMode, bool CanSubmitActions);
}
