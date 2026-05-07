using EscapeRoom.Application.Triggering;
using System.Collections.Concurrent;

namespace EscapeRoom.Infrastructure.Triggering;

public class InMemorySessionLockService : ISessionLockService
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();

    public async Task<SessionLockHandle> AcquireAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var gate = locks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new SessionLockHandle($"inmem-lock:session:{sessionId}", Guid.NewGuid().ToString("N"));
    }

    public Task ReleaseAsync(SessionLockHandle handle)
    {
        if (!TryParseSessionId(handle.Key, out var sessionId))
        {
            return Task.CompletedTask;
        }

        if (locks.TryGetValue(sessionId, out var gate))
        {
            gate.Release();
        }

        return Task.CompletedTask;
    }

    private static bool TryParseSessionId(string key, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        const string prefix = "inmem-lock:session:";
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParse(key[prefix.Length..], out sessionId);
    }
}
