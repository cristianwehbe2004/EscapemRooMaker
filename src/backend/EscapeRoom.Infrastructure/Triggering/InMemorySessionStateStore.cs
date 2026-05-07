using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Triggering;
using System.Collections.Concurrent;

namespace EscapeRoom.Infrastructure.Triggering;

public class InMemorySessionStateStore : ISessionStateStore
{
    private readonly ConcurrentDictionary<Guid, SessionSnapshotEnvelope> snapshots = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<StateDiffEnvelope>> diffs = new();
    private readonly ConcurrentDictionary<Guid, long> sequences = new();
    private const int MaxDiffHistory = 500;

    public Task<long> GetNextDiffSequenceAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var next = sequences.AddOrUpdate(sessionId, 1, (_, current) => current + 1);
        return Task.FromResult(next);
    }

    public Task SaveSnapshotAsync(SessionSnapshotEnvelope snapshot, CancellationToken cancellationToken = default)
    {
        snapshots[snapshot.SessionId] = snapshot;
        return Task.CompletedTask;
    }

    public Task AppendDiffAsync(Guid sessionId, StateDiffEnvelope diff, CancellationToken cancellationToken = default)
    {
        var queue = diffs.GetOrAdd(sessionId, _ => new ConcurrentQueue<StateDiffEnvelope>());
        queue.Enqueue(diff);
        while (queue.Count > MaxDiffHistory && queue.TryDequeue(out _))
        {
        }

        return Task.CompletedTask;
    }

    public Task<SessionSnapshotEnvelope?> GetSnapshotAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        snapshots.TryGetValue(sessionId, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<StateDiffEnvelope>> GetDiffsAfterVersionAsync(Guid sessionId, int lastKnownVersion, CancellationToken cancellationToken = default)
    {
        if (!diffs.TryGetValue(sessionId, out var queue))
        {
            return Task.FromResult<IReadOnlyList<StateDiffEnvelope>>([]);
        }

        var result = queue
            .Where(x => x.SessionVersion > lastKnownVersion)
            .OrderBy(x => x.DiffSequence)
            .ToList();

        return Task.FromResult<IReadOnlyList<StateDiffEnvelope>>(result);
    }
}
