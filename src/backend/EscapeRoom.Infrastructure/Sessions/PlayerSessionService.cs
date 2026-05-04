using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Sessions;
using EscapeRoom.Application.Sessions.Contracts;
using EscapeRoom.Application.Triggering;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Domain.Enums;
using EscapeRoom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Sessions;

public class PlayerSessionService(
    AppDbContext dbContext,
    ISessionStateStore sessionStateStore) : IPlayerSessionService
{
    private const int DefaultDurationMinutes = 60;
    private const int MinDurationMinutes = 5;
    private const int MaxDurationMinutes = 180;

    public async Task<PlayerSessionSummary> CreateSessionAsync(
        CreateSessionRequest request,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var room = await ResolvePublishedRoomAsync(request.RoomId, cancellationToken);
        var now = DateTime.UtcNow;
        var duration = NormalizeDuration(request.DurationMinutes);
        var session = new GameSession
        {
            RoomId = room.Id,
            Status = SessionStatus.Pending,
            StartedAtUtc = now,
            LastActivityAtUtc = now,
            DurationMinutes = duration,
            HostActorId = identity.ActorId,
            IsQuickPlay = false
        };
        session.StateSnapshot = SessionStateFactory.BuildInitialState(room, session, now);

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveRealtimeSnapshotAsync(session, now, cancellationToken);
        return BuildSummary(room, session, identity, now);
    }

    public async Task<PlayerSessionSummary> QuickStartAsync(
        CreateSessionRequest request,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var room = await ResolvePublishedRoomAsync(request.RoomId, cancellationToken);
        var now = DateTime.UtcNow;
        var duration = NormalizeDuration(request.DurationMinutes);
        var session = new GameSession
        {
            RoomId = room.Id,
            Status = SessionStatus.Active,
            StartedAtUtc = now,
            LastActivityAtUtc = now,
            DurationMinutes = duration,
            EndsAtUtc = now.AddMinutes(duration),
            HostActorId = identity.ActorId,
            IsQuickPlay = true
        };
        session.StateSnapshot = SessionStateFactory.BuildInitialState(room, session, now);

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveRealtimeSnapshotAsync(session, now, cancellationToken);
        return BuildSummary(room, session, identity, now);
    }

    public async Task<PlayerSessionSummary> JoinSessionAsync(
        Guid sessionId,
        JoinSessionRequest request,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var (session, room) = await GetSessionWithRoomAsync(sessionId, cancellationToken);
        await RefreshExpirationAsync(session, room, cancellationToken);
        return BuildSummary(room, session, identity, DateTime.UtcNow);
    }

    public async Task<PlayerSessionSummary> StartSessionAsync(
        Guid sessionId,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var (session, room) = await GetSessionWithRoomAsync(sessionId, cancellationToken);
        if (session.Status == SessionStatus.Active)
        {
            return BuildSummary(room, session, identity, DateTime.UtcNow);
        }

        if (session.Status != SessionStatus.Pending)
        {
            throw new InvalidOperationException($"Session '{sessionId}' cannot be started from status '{session.Status}'.");
        }

        if (!string.IsNullOrWhiteSpace(session.HostActorId) &&
            !session.HostActorId.Equals(identity.ActorId, StringComparison.OrdinalIgnoreCase) &&
            identity.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Only the session host can start this session.");
        }

        var now = DateTime.UtcNow;
        session.Status = SessionStatus.Active;
        session.StartedAtUtc = now;
        session.LastActivityAtUtc = now;
        session.EndsAtUtc = now.AddMinutes(session.DurationMinutes);
        session.StateSnapshot = SessionStateFactory.WithSessionState(session.StateSnapshot, room, session, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveRealtimeSnapshotAsync(session, now, cancellationToken);
        return BuildSummary(room, session, identity, now);
    }

    public async Task<PlayerSessionSummary> GetSessionAsync(
        Guid sessionId,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var (session, room) = await GetSessionWithRoomAsync(sessionId, cancellationToken);
        await RefreshExpirationAsync(session, room, cancellationToken);
        return BuildSummary(room, session, identity, DateTime.UtcNow);
    }

    private async Task<Room> ResolvePublishedRoomAsync(Guid? roomId, CancellationToken cancellationToken)
    {
        var query = dbContext.Rooms.Where(x => x.IsPublished);
        var room = roomId.HasValue
            ? await query.FirstOrDefaultAsync(x => x.Id == roomId.Value, cancellationToken)
            : await query.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);

        return room ?? throw new InvalidOperationException("No published room is available to play.");
    }

    private async Task<(GameSession Session, Room Room)> GetSessionWithRoomAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session '{sessionId}' was not found.");
        var room = await dbContext.Rooms.FirstOrDefaultAsync(x => x.Id == session.RoomId, cancellationToken)
            ?? throw new InvalidOperationException($"Room '{session.RoomId}' was not found.");
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

    private static PlayerSessionSummary BuildSummary(Room room, GameSession session, PlayerIdentity identity, DateTime now)
        => new()
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
            DisplayName = identity.DisplayName
        };

    private static int NormalizeDuration(int? durationMinutes)
        => Math.Clamp(durationMinutes ?? DefaultDurationMinutes, MinDurationMinutes, MaxDurationMinutes);
}
