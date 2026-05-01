namespace EscapeRoom.TriggerEngine.Idempotency;

public class NoopIdempotencyStore : IIdempotencyStore
{
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task MarkAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
