using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.Application.Realtime;

public interface ISessionSnapshotHydrator
{
    Task<SessionSnapshotEnvelope?> GetOrHydrateAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
