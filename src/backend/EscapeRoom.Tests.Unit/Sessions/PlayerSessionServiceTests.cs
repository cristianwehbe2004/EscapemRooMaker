using EscapeRoom.Application.Sessions.Contracts;
using EscapeRoom.Application.Triggering;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Domain.Enums;
using EscapeRoom.Infrastructure.Data;
using EscapeRoom.Infrastructure.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EscapeRoom.Tests.Unit.Sessions;

public class PlayerSessionServiceTests
{
    [Fact]
    public async Task CreateSessionAsync_ShouldForceTenMinuteDuration()
    {
        var context = await CreateContextWithPublishedRoomAsync();
        var store = BuildStateStore();
        var service = new PlayerSessionService(context, store.Object);

        var summary = await service.CreateSessionAsync(
            new CreateSessionRequest { DurationMinutes = 60, DisplayName = "Host" },
            new PlayerIdentity { ActorId = "host-1", DisplayName = "Host", IsAuthenticated = false });

        summary.DurationMinutes.Should().Be(10);
        summary.RemainingSeconds.Should().BeGreaterThan(0);
        var session = await context.Sessions.SingleAsync(x => x.Id == summary.SessionId);
        session.DurationMinutes.Should().Be(10);
    }

    [Fact]
    public async Task JoinSessionAsync_ShouldSetSpectatorForNonHostWhenSessionIsAlreadyActive()
    {
        var context = await CreateContextWithPublishedRoomAsync();
        var store = BuildStateStore();
        var service = new PlayerSessionService(context, store.Object);

        var host = new PlayerIdentity { ActorId = "host-2", DisplayName = "Host", IsAuthenticated = false };
        var quickStart = await service.QuickStartAsync(new CreateSessionRequest { DisplayName = "Host" }, host);
        quickStart.Status.Should().Be(SessionStatus.Active.ToString());

        var spectator = await service.JoinSessionAsync(
            quickStart.SessionId,
            new JoinSessionRequest { DisplayName = "Viewer", GuestActorId = "viewer-1" },
            new PlayerIdentity { ActorId = "viewer-1", DisplayName = "Viewer", IsAuthenticated = false });

        spectator.JoinMode.Should().Be("spectator");
        spectator.CanSubmitActions.Should().BeFalse();

        var canSubmit = await service.CanSubmitActionsAsync(quickStart.SessionId, "viewer-1");
        canSubmit.Should().BeFalse();
    }

    private static Mock<ISessionStateStore> BuildStateStore()
    {
        var store = new Mock<ISessionStateStore>();
        store.Setup(x => x.SaveSnapshotAsync(It.IsAny<Application.Realtime.Contracts.SessionSnapshotEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(x => x.GetNextDiffSequenceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        store.Setup(x => x.AppendDiffAsync(It.IsAny<Guid>(), It.IsAny<Application.Realtime.Contracts.StateDiffEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(x => x.GetDiffsAfterVersionAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        store.Setup(x => x.GetSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Application.Realtime.Contracts.SessionSnapshotEnvelope?)null);
        return store;
    }

    private static async Task<AppDbContext> CreateContextWithPublishedRoomAsync()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"player-session-service-{Guid.NewGuid()}")
            .Options;

        var context = new AppDbContext(dbOptions);
        context.Rooms.Add(new Room
        {
            Id = Guid.NewGuid(),
            Name = "Playable Room",
            Description = "seed",
            CreatedByUserId = Guid.NewGuid(),
            IsPublished = true,
            GraphDefinition = "{}",
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return context;
    }
}
