using EscapeRoom.TriggerEngine.Idempotency;
using System.Collections.Concurrent;

namespace EscapeRoom.Infrastructure.Triggering;

public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, DateTime> entries = new(StringComparer.Ordinal);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        CleanupExpired();
        if (!entries.TryGetValue(key, out var expiresAt))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(expiresAt > DateTime.UtcNow);
    }

    public Task MarkAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        entries[key] = DateTime.UtcNow.Add(ttl);
        return Task.CompletedTask;
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in entries)
        {
            if (pair.Value <= now)
            {
                entries.TryRemove(pair.Key, out _);
            }
        }
    }
}
