namespace EscapeRoom.TriggerEngine.Idempotency;

public interface IIdempotencyStore
{
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task MarkAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);
}
