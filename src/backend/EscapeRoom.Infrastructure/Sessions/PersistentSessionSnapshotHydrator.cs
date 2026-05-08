using EscapeRoom.Application.Realtime;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Triggering;
using EscapeRoom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Sessions;

public class PersistentSessionSnapshotHydrator(
    AppDbContext dbContext,
    ISessionStateStore sessionStateStore) : ISessionSnapshotHydrator
{
    public async Task<SessionSnapshotEnvelope?> GetOrHydrateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var cached = await sessionStateStore.GetSnapshotAsync(sessionId, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var session = await dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var latestPersistedSnapshot = await dbContext.SessionSnapshots
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var stateJson = !string.IsNullOrWhiteSpace(session.StateSnapshot) && session.StateSnapshot.Trim() != "{}"
            ? session.StateSnapshot
            : latestPersistedSnapshot?.StateData;
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return null;
        }

        var hydrated = new SessionSnapshotEnvelope
        {
            SessionId = sessionId,
            SessionVersion = latestPersistedSnapshot?.Version ?? 0,
            StateJson = stateJson,
            ServerTimeUtc = session.LastActivityAtUtc
        };

        await sessionStateStore.SaveSnapshotAsync(hydrated, cancellationToken);
        return hydrated;
    }
}
