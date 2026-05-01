using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Infrastructure.Triggering;
using FluentAssertions;
using StackExchange.Redis;

namespace EscapeRoom.Tests.Integration.Triggering;

public class RedisSessionStateStoreTests
{
    [Fact]
    public async Task Store_ShouldPersistSnapshot_AndReplayDiffsInOrder()
    {
        var multiplexer = await ConnectionMultiplexer.ConnectAsync("localhost:6379,abortConnect=false,connectTimeout=1500,connectRetry=1");
        var store = new RedisSessionStateStore(multiplexer);

        var sessionId = Guid.NewGuid();
        var seq1 = await store.GetNextDiffSequenceAsync(sessionId);
        var seq2 = await store.GetNextDiffSequenceAsync(sessionId);

        seq1.Should().Be(1);
        seq2.Should().Be(2);

        await store.SaveSnapshotAsync(new SessionSnapshotEnvelope
        {
            SessionId = sessionId,
            SessionVersion = 2,
            StateJson = "{\"phase\":\"active\"}"
        });

        var snapshot = await store.GetSnapshotAsync(sessionId);
        snapshot.Should().NotBeNull();
        snapshot!.SessionVersion.Should().Be(2);

        await store.AppendDiffAsync(sessionId, new StateDiffEnvelope
        {
            SessionVersion = 1,
            DiffSequence = seq1,
            CorrelationId = "a1"
        });

        await store.AppendDiffAsync(sessionId, new StateDiffEnvelope
        {
            SessionVersion = 2,
            DiffSequence = seq2,
            CorrelationId = "a2"
        });

        var replay = await store.GetDiffsAfterVersionAsync(sessionId, 0);
        replay.Should().HaveCount(2);
        replay[0].DiffSequence.Should().BeLessThan(replay[1].DiffSequence);
    }
}
