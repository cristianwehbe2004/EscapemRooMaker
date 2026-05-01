using EscapeRoom.Application.Realtime.Contracts;

namespace EscapeRoom.Application.Triggering;

public interface ISessionActionProcessor
{
    Task<StateDiffEnvelope> ProcessActionAsync(Guid sessionId, PlayerActionEnvelope action, CancellationToken cancellationToken = default);
}
