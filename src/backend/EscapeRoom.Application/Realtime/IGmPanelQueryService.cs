using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.Application.Realtime;

public interface IGmPanelQueryService
{
    Task<IReadOnlyList<GmSessionSummary>> GetActiveSessionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionTimelineEntry>> GetSessionTimelineAsync(Guid sessionId, int take = 100, CancellationToken cancellationToken = default);
}