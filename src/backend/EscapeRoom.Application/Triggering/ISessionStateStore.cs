using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.Application.Triggering;

public interface ISessionStateStore
{
    Task<long> GetNextDiffSequenceAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task SaveSnapshotAsync(SessionSnapshotEnvelope snapshot, CancellationToken cancellationToken = default);
    Task AppendDiffAsync(Guid sessionId, StateDiffEnvelope diff, CancellationToken cancellationToken = default);
    Task<SessionSnapshotEnvelope?> GetSnapshotAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StateDiffEnvelope>> GetDiffsAfterVersionAsync(Guid sessionId, int lastKnownVersion, CancellationToken cancellationToken = default);
}
