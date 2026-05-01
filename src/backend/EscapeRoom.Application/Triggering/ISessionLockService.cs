namespace EscapeRoom.Application.Triggering;

public interface ISessionLockService
{
    Task<SessionLockHandle> AcquireAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task ReleaseAsync(SessionLockHandle handle);
}

public record SessionLockHandle(string Key, string Token);
