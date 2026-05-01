using System.Security.Claims;
using EscapeRoom.Application.Realtime;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Triggering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using EscapeRoom.Realtime.Presence;
using EscapeRoom.Realtime.RateLimiting;
using System.Text.Json;

namespace EscapeRoom.Realtime.Hubs;

[Authorize]
public class GameHub(
    ISessionActionProcessor sessionActionProcessor,
    ISessionStateStore sessionStateStore,
    IPlayerPresenceTracker playerPresenceTracker,
    IGmPanelQueryService gmPanelQueryService,
    IActionRateLimiter actionRateLimiter) : Hub
{
    public Task Ping() => Clients.Caller.SendAsync("Pong", DateTime.UtcNow);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var disconnected = playerPresenceTracker.TrackDisconnected(Context.ConnectionId);
        if (disconnected is not null)
        {
            await Clients.Group(SessionGroup(disconnected.SessionId)).SendAsync("PlayerPresenceChanged", disconnected);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<JoinSessionAck> JoinSession(Guid sessionId, int? lastKnownVersion = null, CancellationToken cancellationToken = default)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
        var connected = playerPresenceTracker.TrackConnected(sessionId, ResolveActor(), ResolveDisplayName(), Context.ConnectionId);
        await Clients.Group(SessionGroup(sessionId)).SendAsync("PlayerPresenceChanged", connected, cancellationToken);

        var replayCount = 0;
        if (lastKnownVersion.HasValue)
        {
            var replayDiffs = await sessionStateStore.GetDiffsAfterVersionAsync(sessionId, lastKnownVersion.Value, cancellationToken);
            foreach (var diff in replayDiffs.OrderBy(x => x.DiffSequence))
            {
                await Clients.Caller.SendAsync("StateDiff", diff, cancellationToken);
                replayCount++;
            }
        }

        if (replayCount == 0)
        {
            var snapshot = await sessionStateStore.GetSnapshotAsync(sessionId, cancellationToken);
            if (snapshot is not null)
            {
                AttachPresence(sessionId, snapshot);
                await Clients.Caller.SendAsync("SessionSnapshot", snapshot, cancellationToken);
            }
        }

        return new JoinSessionAck
        {
            SessionId = sessionId,
            ReplayedDiffCount = replayCount,
            LastKnownVersion = lastKnownVersion,
            CurrentVersion = (await sessionStateStore.GetSnapshotAsync(sessionId, cancellationToken))?.SessionVersion ?? 0
        };
    }

    public async Task<LeaveSessionAck> LeaveSession(Guid sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
        var disconnected = playerPresenceTracker.TrackDisconnected(Context.ConnectionId);
        if (disconnected is not null)
        {
            await Clients.Group(SessionGroup(disconnected.SessionId)).SendAsync("PlayerPresenceChanged", disconnected);
        }

        return new LeaveSessionAck { SessionId = sessionId };
    }

    public async Task<StateDiffEnvelope> SubmitAction(Guid sessionId, PlayerActionEnvelope action, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action.Actor))
        {
            action.Actor = ResolveActor();
        }

        var rateLimitDecision = actionRateLimiter.Evaluate(sessionId, action);
        if (!rateLimitDecision.Allowed)
        {
            var error = ActionRateLimitError.FromDecision(rateLimitDecision, action);
            throw new HubException(JsonSerializer.Serialize(error));
        }

        var diff = await sessionActionProcessor.ProcessActionAsync(sessionId, action, cancellationToken);
        await Clients.Group(SessionGroup(sessionId)).SendAsync("StateDiff", diff, cancellationToken);
        return diff;
    }

    [Authorize(Policy = "GMOnly")]
    public Task<StateDiffEnvelope> SubmitGmHint(Guid sessionId, GmHintAction hint, CancellationToken cancellationToken = default)
        => SubmitAction(sessionId, BuildGmAction(
            "gm.hint",
            hint.Target,
            new Dictionary<string, object?>
            {
                ["hint"] = hint.Hint,
                ["scope"] = hint.Scope
            },
            hint.ClientActionId), cancellationToken);

    [Authorize(Policy = "GMOnly")]
    public Task<StateDiffEnvelope> SubmitGmControl(Guid sessionId, GmControlAction control, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(control.ControlType))
        {
            throw new ArgumentException("Control type is required.", nameof(control));
        }

        var normalized = control.ControlType.Trim().ToLowerInvariant();
        var actionType = normalized.StartsWith("gm.", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"gm.{normalized}";

        return SubmitAction(sessionId, BuildGmAction(actionType, control.Target, control.Payload, control.ClientActionId), cancellationToken);
    }

    [Authorize(Policy = "GMOnly")]
    public async Task<StateDiffEnvelope> ForceSyncSession(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var diff = await SubmitGmControl(sessionId, new GmControlAction
        {
            ControlType = "force_sync",
            Payload = new Dictionary<string, object?>
            {
                ["requestedBy"] = ResolveActor()
            }
        }, cancellationToken);

        var snapshot = await RequestSnapshot(sessionId, cancellationToken);
        await Clients.Group(SessionGroup(sessionId)).SendAsync("SessionSnapshot", snapshot, cancellationToken);
        return diff;
    }

    [Authorize(Policy = "GMOnly")]
    public Task<StateDiffEnvelope> RevealPuzzle(Guid sessionId, string puzzleId, string? target = null, CancellationToken cancellationToken = default)
        => SubmitGmControl(sessionId, new GmControlAction
        {
            ControlType = "reveal",
            Target = target,
            Payload = new Dictionary<string, object?>
            {
                ["puzzleId"] = puzzleId
            }
        }, cancellationToken);

    [Authorize(Policy = "GMOnly")]
    public Task<StateDiffEnvelope> BroadcastMessage(Guid sessionId, string message, string? target = null, CancellationToken cancellationToken = default)
        => SubmitGmControl(sessionId, new GmControlAction
        {
            ControlType = "broadcast",
            Target = target,
            Payload = new Dictionary<string, object?>
            {
                ["message"] = message
            }
        }, cancellationToken);

    [Authorize(Policy = "GMOnly")]
    public async Task<IReadOnlyList<GmSessionSummary>> GetActiveSessions(CancellationToken cancellationToken = default)
    {
        var sessions = (await gmPanelQueryService.GetActiveSessionsAsync(cancellationToken)).ToList();
        foreach (var session in sessions)
        {
            session.ConnectedPlayers = playerPresenceTracker.GetConnectedCount(session.SessionId);
        }

        return sessions;
    }

    [Authorize(Policy = "GMOnly")]
    public Task<IReadOnlyList<SessionTimelineEntry>> GetSessionTimeline(Guid sessionId, int take = 100, CancellationToken cancellationToken = default)
        => gmPanelQueryService.GetSessionTimelineAsync(sessionId, take, cancellationToken);

    [Authorize(Policy = "GMOnly")]
    public Task<IReadOnlyList<PlayerPresenceEvent>> GetPlayerPresence(Guid sessionId)
        => Task.FromResult(playerPresenceTracker.GetSessionPresence(sessionId));

    public async Task<SessionSnapshotEnvelope> RequestSnapshot(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var snapshot = await sessionStateStore.GetSnapshotAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"No snapshot found for session '{sessionId}'.");
        AttachPresence(sessionId, snapshot);
        return snapshot;
    }

    public async Task<RecoverSessionResult> RecoverSession(Guid sessionId, int lastKnownVersion, CancellationToken cancellationToken = default)
    {
        var replayDiffs = await sessionStateStore.GetDiffsAfterVersionAsync(sessionId, lastKnownVersion, cancellationToken);
        foreach (var diff in replayDiffs.OrderBy(x => x.DiffSequence))
        {
            await Clients.Caller.SendAsync("StateDiff", diff, cancellationToken);
        }

        if (replayDiffs.Count == 0)
        {
            var snapshot = await sessionStateStore.GetSnapshotAsync(sessionId, cancellationToken)
                ?? throw new InvalidOperationException($"No snapshot found for session '{sessionId}'.");
            AttachPresence(sessionId, snapshot);
            await Clients.Caller.SendAsync("SessionSnapshot", snapshot, cancellationToken);

            return new RecoverSessionResult
            {
                SessionId = sessionId,
                ReplayedDiffCount = 0,
                SnapshotSent = true,
                CurrentVersion = snapshot.SessionVersion
            };
        }

        return new RecoverSessionResult
        {
            SessionId = sessionId,
            ReplayedDiffCount = replayDiffs.Count,
            SnapshotSent = false,
            CurrentVersion = replayDiffs.Max(x => x.SessionVersion)
        };
    }

    private string ResolveActor()
        => Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub")
            ?? "unknown";

    private string ResolveDisplayName()
        => Context.User?.FindFirstValue(ClaimTypes.Name)
            ?? Context.User?.FindFirstValue(ClaimTypes.Email)
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? ResolveActor();

    private PlayerActionEnvelope BuildGmAction(
        string actionType,
        string? target,
        Dictionary<string, object?> payload,
        string? clientActionId = null)
        => new()
        {
            ActionType = actionType,
            Actor = ResolveActor(),
            Target = target,
            ClientActionId = string.IsNullOrWhiteSpace(clientActionId) ? Guid.NewGuid().ToString("N") : clientActionId,
            TimestampUtc = DateTime.UtcNow,
            Payload = payload
        };

    private void AttachPresence(Guid sessionId, SessionSnapshotEnvelope snapshot)
    {
        snapshot.PlayerPresence = playerPresenceTracker.GetSessionPresence(sessionId).ToList();
    }

    private static string SessionGroup(Guid sessionId) => $"session:{sessionId}";
}
