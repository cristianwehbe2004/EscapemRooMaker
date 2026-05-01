using EscapeRoom.Application.Triggering;
using StackExchange.Redis;

namespace EscapeRoom.Infrastructure.Triggering;

public class RedisSessionLockService(IConnectionMultiplexer multiplexer) : ISessionLockService
{
    private const string ReleaseScript = """
        if redis.call("GET", KEYS[1]) == ARGV[1] then
            return redis.call("DEL", KEYS[1])
        else
            return 0
        end
        """;

    public async Task<SessionLockHandle> AcquireAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        var key = $"lock:session:{sessionId}";
        var token = Guid.NewGuid().ToString("N");
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await db.StringSetAsync(key, token, TimeSpan.FromSeconds(10), When.NotExists))
            {
                return new SessionLockHandle(key, token);
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new InvalidOperationException($"Could not acquire lock for session '{sessionId}'.");
    }

    public async Task ReleaseAsync(SessionLockHandle handle)
    {
        var db = multiplexer.GetDatabase();
        await db.ScriptEvaluateAsync(
            ReleaseScript,
            [new RedisKey(handle.Key)],
            [new RedisValue(handle.Token)]);
    }
}
