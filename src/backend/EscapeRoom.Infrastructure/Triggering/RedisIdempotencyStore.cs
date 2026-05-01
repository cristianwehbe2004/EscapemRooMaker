using EscapeRoom.TriggerEngine.Idempotency;
using StackExchange.Redis;

namespace EscapeRoom.Infrastructure.Triggering;

public class RedisIdempotencyStore(IConnectionMultiplexer multiplexer) : IIdempotencyStore
{
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        return await db.KeyExistsAsync(key);
    }

    public async Task MarkAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        await db.StringSetAsync(key, "1", ttl, when: When.NotExists);
    }
}
