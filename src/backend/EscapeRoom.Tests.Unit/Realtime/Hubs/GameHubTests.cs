using System.Security.Claims;
using System.Text.Json;
using EscapeRoom.Application.Realtime;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Sessions;
using EscapeRoom.Application.Triggering;
using EscapeRoom.Realtime.Hubs;
using EscapeRoom.Realtime.Presence;
using EscapeRoom.Realtime.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace EscapeRoom.Tests.Unit.Realtime.Hubs;

public class GameHubTests
{
    [Fact]
    public async Task SubmitGmHint_ShouldMapToPlayerActionAndBroadcast()
    {
        var sessionId = Guid.NewGuid();
        var processor = new Mock<ISessionActionProcessor>();
        processor.Setup(x => x.ProcessActionAsync(sessionId, It.IsAny<PlayerActionEnvelope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StateDiffEnvelope { SessionVersion = 2, DiffSequence = 1 });

        var hub = BuildHub(
            processor,
            BuildAllowingRateLimiter(),
            BuildAllowingPlayerSessionService(),
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "gm-user"),
                new Claim(ClaimTypes.Role, "GM")
            ])));

        var result = await hub.SubmitGmHint(sessionId, new GmHintAction { Hint = "Try the bookshelf", Target = "team-a" });

        result.SessionVersion.Should().Be(2);
        processor.Verify(x => x.ProcessActionAsync(
            sessionId,
            It.Is<PlayerActionEnvelope>(a =>
                a.ActionType == "gm.hint" &&
                a.Actor == "gm-user" &&
                a.Target == "team-a" &&
                a.Payload.ContainsKey("hint")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinSession_ShouldReplayOrderedDiffs_WhenVersionProvided()
    {
        var sessionId = Guid.NewGuid();
        var processor = new Mock<ISessionActionProcessor>();
        var store = new Mock<ISessionStateStore>();
        var hydrator = new Mock<ISessionSnapshotHydrator>();
        var presenceTracker = new Mock<IPlayerPresenceTracker>();
        var gmPanelQueryService = new Mock<IGmPanelQueryService>();

        presenceTracker.Setup(x => x.TrackConnected(sessionId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new PlayerPresenceEvent
            {
                SessionId = sessionId,
                PlayerId = "player-1",
                DisplayName = "player-1",
                Status = "connected",
                IsConnected = true,
                ConnectedAtUtc = DateTime.UtcNow,
                LastSeenUtc = DateTime.UtcNow
            });

        store.Setup(x => x.GetDiffsAfterVersionAsync(sessionId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StateDiffEnvelope { SessionVersion = 4, DiffSequence = 4 },
                new StateDiffEnvelope { SessionVersion = 3, DiffSequence = 3 }
            ]);
        hydrator.Setup(x => x.GetOrHydrateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionSnapshotEnvelope { SessionId = sessionId, SessionVersion = 4 });

        var callerProxy = new Mock<ISingleClientProxy>();
        callerProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(x => x.Caller).Returns(callerProxy.Object);
        clients.Setup(x => x.Group(It.IsAny<string>())).Returns(groupProxy.Object);

        var groups = new Mock<IGroupManager>();
        groups.Setup(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.ConnectionId).Returns("conn-1");
        context.SetupGet(x => x.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));

        var hub = new GameHub(
            processor.Object,
            store.Object,
            hydrator.Object,
            BuildAllowingPlayerSessionService().Object,
            presenceTracker.Object,
            gmPanelQueryService.Object,
            BuildAllowingRateLimiter().Object,
            new Mock<ILogger<GameHub>>().Object)
        {
            Clients = clients.Object,
            Groups = groups.Object,
            Context = context.Object
        };

        var ack = await hub.JoinSession(sessionId, 2);

        ack.ReplayedDiffCount.Should().Be(2);
        ack.CurrentVersion.Should().Be(4);
        callerProxy.Verify(x => x.SendCoreAsync("StateDiff", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SubmitAction_ShouldThrowStructuredHubException_WhenRateLimited()
    {
        var sessionId = Guid.NewGuid();
        var processor = new Mock<ISessionActionProcessor>();
        var rateLimiter = new Mock<IActionRateLimiter>();
        rateLimiter
            .Setup(x => x.Evaluate(It.IsAny<Guid>(), It.IsAny<PlayerActionEnvelope>(), It.IsAny<ActionRateLimitContext>()))
            .Returns(new ActionRateLimitDecision(false, 1200, "player-action-default", "player", "player:action"));

        var hub = BuildHub(processor, rateLimiter, BuildAllowingPlayerSessionService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SubmitAction(sessionId, new PlayerActionEnvelope
        {
            ActionType = "inspect",
            Actor = "player-1",
            Target = "desk-note",
            Payload = new Dictionary<string, object?>()
        }));

        var payload = JsonSerializer.Deserialize<ActionRateLimitError>(ex.Message);
        payload.Should().NotBeNull();
        payload!.Code.Should().Be("rate_limited");

        processor.Verify(
            x => x.ProcessActionAsync(It.IsAny<Guid>(), It.IsAny<PlayerActionEnvelope>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitAction_ShouldThrow_WhenActorIsSpectator()
    {
        var sessionId = Guid.NewGuid();
        var processor = new Mock<ISessionActionProcessor>();
        var playerSessionService = new Mock<IPlayerSessionService>();
        playerSessionService
            .Setup(x => x.CanSubmitActionsAsync(sessionId, "spectator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var hub = BuildHub(processor, BuildAllowingRateLimiter(), playerSessionService, new ClaimsPrincipal(new ClaimsIdentity()));

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SubmitAction(sessionId, new PlayerActionEnvelope
        {
            ActionType = "inspect",
            Actor = "spectator-1",
            Target = "desk-note",
            Payload = new Dictionary<string, object?>()
        }));

        ex.Message.ToLowerInvariant().Should().Contain("spectator mode");
        processor.Verify(x => x.ProcessActionAsync(It.IsAny<Guid>(), It.IsAny<PlayerActionEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecoverSession_ShouldSendSnapshot_WhenNoDiffsExist()
    {
        var sessionId = Guid.NewGuid();
        var processor = new Mock<ISessionActionProcessor>();
        var store = new Mock<ISessionStateStore>();
        var hydrator = new Mock<ISessionSnapshotHydrator>();
        var presenceTracker = new Mock<IPlayerPresenceTracker>();

        store.Setup(x => x.GetDiffsAfterVersionAsync(sessionId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        presenceTracker.Setup(x => x.GetSessionPresence(sessionId)).Returns([]);
        hydrator.Setup(x => x.GetOrHydrateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionSnapshotEnvelope
            {
                SessionId = sessionId,
                SessionVersion = 6,
                StateJson = "{}"
            });

        var callerProxy = new Mock<ISingleClientProxy>();
        callerProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(x => x.Caller).Returns(callerProxy.Object);
        clients.Setup(x => x.Group(It.IsAny<string>())).Returns(groupProxy.Object);

        var hub = new GameHub(
            processor.Object,
            store.Object,
            hydrator.Object,
            BuildAllowingPlayerSessionService().Object,
            presenceTracker.Object,
            new Mock<IGmPanelQueryService>().Object,
            BuildAllowingRateLimiter().Object,
            new Mock<ILogger<GameHub>>().Object)
        {
            Clients = clients.Object,
            Groups = Mock.Of<IGroupManager>(),
            Context = Mock.Of<HubCallerContext>()
        };

        var result = await hub.RecoverSession(sessionId, 5);

        result.ReplayedDiffCount.Should().Be(0);
        result.SnapshotSent.Should().BeTrue();
        result.CurrentVersion.Should().Be(6);
        callerProxy.Verify(x => x.SendCoreAsync("SessionSnapshot", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GameHub BuildHub(
        Mock<ISessionActionProcessor> processor,
        Mock<IActionRateLimiter> rateLimiter,
        Mock<IPlayerSessionService> playerSessionService,
        ClaimsPrincipal principal)
    {
        var store = new Mock<ISessionStateStore>();
        var hydrator = new Mock<ISessionSnapshotHydrator>();
        var presenceTracker = new Mock<IPlayerPresenceTracker>();
        var gmPanelQueryService = new Mock<IGmPanelQueryService>();

        var clients = new Mock<IHubCallerClients>();
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var callerProxy = new Mock<ISingleClientProxy>();
        callerProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        clients.Setup(x => x.Group(It.IsAny<string>())).Returns(groupProxy.Object);
        clients.SetupGet(x => x.Caller).Returns(callerProxy.Object);

        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.ConnectionId).Returns("conn-1");
        context.SetupGet(x => x.User).Returns(principal);

        return new GameHub(
            processor.Object,
            store.Object,
            hydrator.Object,
            playerSessionService.Object,
            presenceTracker.Object,
            gmPanelQueryService.Object,
            rateLimiter.Object,
            new Mock<ILogger<GameHub>>().Object)
        {
            Clients = clients.Object,
            Groups = new Mock<IGroupManager>().Object,
            Context = context.Object
        };
    }

    private static Mock<IActionRateLimiter> BuildAllowingRateLimiter()
    {
        var rateLimiter = new Mock<IActionRateLimiter>();
        rateLimiter
            .Setup(x => x.Evaluate(It.IsAny<Guid>(), It.IsAny<PlayerActionEnvelope>(), It.IsAny<ActionRateLimitContext>()))
            .Returns(new ActionRateLimitDecision(true, 0, "player-action-default", "player", "player:action"));
        return rateLimiter;
    }

    private static Mock<IPlayerSessionService> BuildAllowingPlayerSessionService()
    {
        var service = new Mock<IPlayerSessionService>();
        service
            .Setup(x => x.CanSubmitActionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return service;
    }

    [Fact]
    public async Task JoinSession_ShouldHydrateSnapshot_WhenTransientStoreIsEmpty()
    {
        var sessionId = Guid.NewGuid();
        var store = new Mock<ISessionStateStore>();
        var hydrator = new Mock<ISessionSnapshotHydrator>();
        var presenceTracker = new Mock<IPlayerPresenceTracker>();

        presenceTracker.Setup(x => x.TrackConnected(sessionId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new PlayerPresenceEvent
            {
                SessionId = sessionId,
                PlayerId = "player-1",
                DisplayName = "player-1",
                Status = "connected",
                IsConnected = true,
                ConnectedAtUtc = DateTime.UtcNow,
                LastSeenUtc = DateTime.UtcNow
            });
        store.Setup(x => x.GetDiffsAfterVersionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        hydrator.Setup(x => x.GetOrHydrateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionSnapshotEnvelope
            {
                SessionId = sessionId,
                SessionVersion = 2,
                StateJson = "{}"
            });

        var callerProxy = new Mock<ISingleClientProxy>();
        callerProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(x => x.Caller).Returns(callerProxy.Object);
        clients.Setup(x => x.Group(It.IsAny<string>())).Returns(groupProxy.Object);

        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.ConnectionId).Returns("conn-1");
        context.SetupGet(x => x.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));

        var hub = new GameHub(
            new Mock<ISessionActionProcessor>().Object,
            store.Object,
            hydrator.Object,
            BuildAllowingPlayerSessionService().Object,
            presenceTracker.Object,
            new Mock<IGmPanelQueryService>().Object,
            BuildAllowingRateLimiter().Object,
            new Mock<ILogger<GameHub>>().Object)
        {
            Clients = clients.Object,
            Groups = Mock.Of<IGroupManager>(),
            Context = context.Object
        };

        var ack = await hub.JoinSession(sessionId, 0);

        ack.CurrentVersion.Should().Be(2);
        callerProxy.Verify(x => x.SendCoreAsync("SessionSnapshot", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestSnapshot_ShouldHydrateSnapshot_WhenTransientStoreIsEmpty()
    {
        var sessionId = Guid.NewGuid();
        var hydrator = new Mock<ISessionSnapshotHydrator>();
        hydrator.Setup(x => x.GetOrHydrateAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionSnapshotEnvelope
            {
                SessionId = sessionId,
                SessionVersion = 3,
                StateJson = "{}"
            });

        var hub = new GameHub(
            new Mock<ISessionActionProcessor>().Object,
            new Mock<ISessionStateStore>().Object,
            hydrator.Object,
            BuildAllowingPlayerSessionService().Object,
            Mock.Of<IPlayerPresenceTracker>(),
            Mock.Of<IGmPanelQueryService>(),
            BuildAllowingRateLimiter().Object,
            new Mock<ILogger<GameHub>>().Object)
        {
            Clients = Mock.Of<IHubCallerClients>(),
            Groups = Mock.Of<IGroupManager>(),
            Context = Mock.Of<HubCallerContext>()
        };

        var snapshot = await hub.RequestSnapshot(sessionId);

        snapshot.SessionVersion.Should().Be(3);
    }
}
