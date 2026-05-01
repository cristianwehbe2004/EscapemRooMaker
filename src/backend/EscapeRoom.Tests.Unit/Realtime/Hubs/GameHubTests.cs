using System.Security.Claims;
using System.Text.Json;
using EscapeRoom.Application.Realtime;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Triggering;
using EscapeRoom.Realtime.Hubs;
using EscapeRoom.Realtime.Presence;
using EscapeRoom.Realtime.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
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

        var store = new Mock<ISessionStateStore>();
        var presenceTracker = new Mock<IPlayerPresenceTracker>();
        var gmPanelQueryService = new Mock<IGmPanelQueryService>();
        var rateLimiter = BuildAllowingRateLimiter();
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var callerProxy = new Mock<ISingleClientProxy>();
        callerProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.Setup(x => x.Group(It.IsAny<string>())).Returns(groupProxy.Object);
        clients.SetupGet(x => x.Caller).Returns(callerProxy.Object);

        var groups = new Mock<IGroupManager>();
        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.ConnectionId).Returns("conn-1");
        context.SetupGet(x => x.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "gm-user"),
            new Claim(ClaimTypes.Role, "GM")
        ])));

        var hub = new GameHub(processor.Object, store.Object, presenceTracker.Object, gmPanelQueryService.Object, rateLimiter.Object)
        {
            Clients = clients.Object,
            Groups = groups.Object,
            Context = context.Object
        };

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
        var presenceTracker = new Mock<IPlayerPresenceTracker>();
        var gmPanelQueryService = new Mock<IGmPanelQueryService>();
        var rateLimiter = BuildAllowingRateLimiter();

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
            .ReturnsAsync(new List<StateDiffEnvelope>
            {
                new() { SessionVersion = 4, DiffSequence = 4 },
                new() { SessionVersion = 3, DiffSequence = 3 }
            });
        store.Setup(x => x.GetSnapshotAsync(sessionId, It.IsAny<CancellationToken>()))
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

        var hub = new GameHub(processor.Object, store.Object, presenceTracker.Object, gmPanelQueryService.Object, rateLimiter.Object)
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
        var store = new Mock<ISessionStateStore>();
        var presenceTracker = new Mock<IPlayerPresenceTracker>();
        var gmPanelQueryService = new Mock<IGmPanelQueryService>();
        var rateLimiter = new Mock<IActionRateLimiter>();
        rateLimiter
            .Setup(x => x.Evaluate(It.IsAny<Guid>(), It.IsAny<PlayerActionEnvelope>()))
            .Returns(new ActionRateLimitDecision(false, 1200, "player-action-default"));

        var clients = new Mock<IHubCallerClients>();
        var groups = new Mock<IGroupManager>();
        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.ConnectionId).Returns("conn-1");
        context.SetupGet(x => x.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));

        var hub = new GameHub(processor.Object, store.Object, presenceTracker.Object, gmPanelQueryService.Object, rateLimiter.Object)
        {
            Clients = clients.Object,
            Groups = groups.Object,
            Context = context.Object
        };

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
        payload.PolicyName.Should().Be("player-action-default");
        payload.RetryAfterMs.Should().Be(1200);

        processor.Verify(
            x => x.ProcessActionAsync(It.IsAny<Guid>(), It.IsAny<PlayerActionEnvelope>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<IActionRateLimiter> BuildAllowingRateLimiter()
    {
        var rateLimiter = new Mock<IActionRateLimiter>();
        rateLimiter
            .Setup(x => x.Evaluate(It.IsAny<Guid>(), It.IsAny<PlayerActionEnvelope>()))
            .Returns(new ActionRateLimitDecision(true, 0, "player-action-default"));
        return rateLimiter;
    }
}
