using EscapeRoom.Infrastructure.Triggering;
using FluentAssertions;
using StackExchange.Redis;

namespace EscapeRoom.Tests.Integration.Triggering;

public class RedisSessionLockServiceTests
{
    [Fact]
    public async Task AcquireAsync_ShouldEnforceSingleWriterLock()
    {
        var multiplexer = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        var service = new RedisSessionLockService(multiplexer);
        var sessionId = Guid.NewGuid();

        var first = await service.AcquireAsync(sessionId);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AcquireAsync(sessionId, cts.Token));

        await service.ReleaseAsync(first);
        var second = await service.AcquireAsync(sessionId);
        second.Should().NotBeNull();
        await service.ReleaseAsync(second);
    }
}
