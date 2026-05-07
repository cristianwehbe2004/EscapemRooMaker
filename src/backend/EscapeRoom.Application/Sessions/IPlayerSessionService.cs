using EscapeRoom.Application.Sessions.Contracts;

namespace EscapeRoom.Application.Sessions;

public interface IPlayerSessionService
{
    Task<PlayerSessionSummary> CreateSessionAsync(
        CreateSessionRequest request,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default);

    Task<PlayerSessionSummary> QuickStartAsync(
        CreateSessionRequest request,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default);

    Task<PlayerSessionSummary> JoinSessionAsync(
        Guid sessionId,
        JoinSessionRequest request,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default);

    Task<PlayerSessionSummary> StartSessionAsync(
        Guid sessionId,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default);

    Task<PlayerSessionSummary> GetSessionAsync(
        Guid sessionId,
        PlayerIdentity identity,
        CancellationToken cancellationToken = default);

    Task<bool> CanSubmitActionsAsync(
        Guid sessionId,
        string actorId,
        CancellationToken cancellationToken = default);
}
