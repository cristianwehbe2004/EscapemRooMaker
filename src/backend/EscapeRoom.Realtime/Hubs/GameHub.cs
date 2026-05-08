using System.Security.Claims;
using EscapeRoom.Application.Realtime;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Sessions;
using EscapeRoom.Application.Triggering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using EscapeRoom.Realtime.Presence;
using EscapeRoom.Realtime.RateLimiting;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EscapeRoom.Realtime.Hubs;

public class GameHub(
    ISessionActionProcessor sessionActionProcessor,
    ISessionStateStore sessionStateStore,
    ISessionSnapshotHydrator sessionSnapshotHydrator,
    IPlayerSessionService playerSessionService,
    IPlayerPresenceTracker playerPresenceTracker,
    IGmPanelQueryService gmPanelQueryService,
    IActionRateLimiter actionRateLimiter,
    ILogger<GameHub> logger) : Hub
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

    public async Task<JoinSessionAck> JoinSession(
        Guid sessionId,
        int? lastKnownVersion = null,
        string? displayName = null,
        string? guestActorId = null)
    {
        var cancellationToken = Context.ConnectionAborted;
        logger.LogWarning(
            "JoinSession requested. SessionId={SessionId} ConnectionId={ConnectionId} LastKnownVersion={LastKnownVersion} DisplayName={DisplayName} GuestActorId={GuestActorId}",
            sessionId,
            Context.ConnectionId,
            lastKnownVersion,
            displayName,
            guestActorId);

        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
            logger.LogWarning("JoinSession group attach succeeded. SessionId={SessionId} ConnectionId={ConnectionId}", sessionId, Context.ConnectionId);

            var connected = playerPresenceTracker.TrackConnected(
                sessionId,
                ResolveActor(guestActorId),
                ResolveDisplayName(displayName, guestActorId),
                Context.ConnectionId);
            await Clients.Group(SessionGroup(sessionId)).SendAsync("PlayerPresenceChanged", connected, cancellationToken);
            logger.LogWarning(
                "JoinSession presence tracked. SessionId={SessionId} ConnectionId={ConnectionId} ActorId={ActorId} DisplayName={ResolvedDisplayName}",
                sessionId,
                Context.ConnectionId,
                connected.PlayerId,
                connected.DisplayName);

            var replayCount = 0;
            var currentVersion = 0;

            if (lastKnownVersion.HasValue)
            {
                try
                {
                    var replayDiffs = await sessionStateStore.GetDiffsAfterVersionAsync(sessionId, lastKnownVersion.Value, cancellationToken);
                    logger.LogWarning(
                        "JoinSession replay lookup complete. SessionId={SessionId} LastKnownVersion={LastKnownVersion} ReplayCount={ReplayCount}",
                        sessionId,
                        lastKnownVersion.Value,
                        replayDiffs.Count);
                    foreach (var diff in replayDiffs.OrderBy(x => x.DiffSequence))
                    {
                        await Clients.Caller.SendAsync("StateDiff", diff, cancellationToken);
                        replayCount++;
                        currentVersion = Math.Max(currentVersion, diff.SessionVersion);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "JoinSession replay failed; falling back to snapshot. SessionId={SessionId} LastKnownVersion={LastKnownVersion}",
                        sessionId,
                        lastKnownVersion.Value);
                }
            }

            if (replayCount == 0)
            {
                try
                {
                    var snapshot = await sessionSnapshotHydrator.GetOrHydrateAsync(sessionId, cancellationToken);
                    if (snapshot is not null)
                    {
                        AttachPresence(sessionId, snapshot);
                        await Clients.Caller.SendAsync("SessionSnapshot", snapshot, cancellationToken);
                        currentVersion = snapshot.SessionVersion;
                        logger.LogWarning(
                            "JoinSession snapshot sent. SessionId={SessionId} SnapshotVersion={SnapshotVersion}",
                            sessionId,
                            snapshot.SessionVersion);
                    }
                    else
                    {
                        logger.LogWarning("JoinSession snapshot not found. SessionId={SessionId}", sessionId);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "JoinSession snapshot fallback failed. SessionId={SessionId}", sessionId);
                }
            }

            logger.LogWarning(
                "JoinSession completed. SessionId={SessionId} ConnectionId={ConnectionId} ReplayedDiffCount={ReplayedDiffCount} CurrentVersion={CurrentVersion}",
                sessionId,
                Context.ConnectionId,
                replayCount,
                currentVersion);

            return new JoinSessionAck
            {
                SessionId = sessionId,
                ReplayedDiffCount = replayCount,
                LastKnownVersion = lastKnownVersion,
                CurrentVersion = currentVersion
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "JoinSession failed. SessionId={SessionId} ConnectionId={ConnectionId} LastKnownVersion={LastKnownVersion}",
                sessionId,
                Context.ConnectionId,
                lastKnownVersion);
            throw new HubException($"JoinSession failed: {ex.Message}");
        }
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

    public async Task<StateDiffEnvelope> SubmitAction(Guid sessionId, PlayerActionEnvelope action)
    {
        var cancellationToken = Context.ConnectionAborted;
        if (string.IsNullOrWhiteSpace(action.Actor))
        {
            action.Actor = ResolveActor();
        }

        var canSubmitActions = await playerSessionService.CanSubmitActionsAsync(sessionId, action.Actor, cancellationToken);
        if (!canSubmitActions)
        {
            throw new HubException("Action blocked: actor is currently in spectator mode.");
        }

        var rateLimitDecision = actionRateLimiter.Evaluate(sessionId, action, new ActionRateLimitContext
        {
            PolicyScope = ResolvePolicyScope(action),
            ActorRole = ResolveActorRole()
        });
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
    public Task<StateDiffEnvelope> SubmitGmHint(Guid sessionId, GmHintAction hint)
        => SubmitAction(sessionId, BuildGmAction(
            "gm.hint",
            hint.Target,
            new Dictionary<string, object?>
            {
                ["hint"] = hint.Hint,
                ["scope"] = hint.Scope
            },
            hint.ClientActionId));

    [Authorize(Policy = "GMOnly")]
    public Task<StateDiffEnvelope> SubmitGmControl(Guid sessionId, GmControlAction control)
    {
        if (string.IsNullOrWhiteSpace(control.ControlType))
        {
            throw new ArgumentException("Control type is required.", nameof(control));
        }

        var normalized = control.ControlType.Trim().ToLowerInvariant();
        var actionType = normalized.StartsWith("gm.", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"gm.{normalized}";

        return SubmitAction(sessionId, BuildGmAction(actionType, control.Target, control.Payload, control.ClientActionId));
    }

    [Authorize(Policy = "GMOnly")]
    public async Task<StateDiffEnvelope> ForceSyncSession(Guid sessionId)
    {
        var cancellationToken = Context.ConnectionAborted;
        var diff = await SubmitGmControl(sessionId, new GmControlAction
        {
            ControlType = "force_sync",
            Payload = new Dictionary<string, object?>
            {
                ["requestedBy"] = ResolveActor()
            }
        });

        var snapshot = await RequestSnapshot(sessionId);
        await Clients.Group(SessionGroup(sessionId)).SendAsync("SessionSnapshot", snapshot, cancellationToken);
        return diff;
    }

    [Authorize(Policy = "GMOnly")]
    public Task<StateDiffEnvelope> RevealPuzzle(Guid sessionId, string puzzleId, string? target = null)
        => SubmitGmControl(sessionId, new GmControlAction
        {
            ControlType = "reveal",
            Target = target,
            Payload = new Dictionary<string, object?>
            {
                ["puzzleId"] = puzzleId
            }
        });

    [Authorize(Policy = "GMOnly")]
    public Task<StateDiffEnvelope> BroadcastMessage(Guid sessionId, string message, string? target = null)
        => SubmitGmControl(sessionId, new GmControlAction
        {
            ControlType = "broadcast",
            Target = target,
            Payload = new Dictionary<string, object?>
            {
                ["message"] = message
            }
        });

    [Authorize(Policy = "GMOnly")]
    public async Task<IReadOnlyList<GmSessionSummary>> GetActiveSessions()
    {
        var cancellationToken = Context.ConnectionAborted;
        var sessions = (await gmPanelQueryService.GetActiveSessionsAsync(cancellationToken)).ToList();
        foreach (var session in sessions)
        {
            session.ConnectedPlayers = playerPresenceTracker.GetConnectedCount(session.SessionId);
        }

        return sessions;
    }

    [Authorize(Policy = "GMOnly")]
    public Task<IReadOnlyList<SessionTimelineEntry>> GetSessionTimeline(Guid sessionId, int take = 100)
    {
        var cancellationToken = Context.ConnectionAborted;
        return gmPanelQueryService.GetSessionTimelineAsync(sessionId, take, cancellationToken);
    }

    [Authorize(Policy = "GMOnly")]
    public Task<IReadOnlyList<PlayerPresenceEvent>> GetPlayerPresence(Guid sessionId)
        => Task.FromResult(playerPresenceTracker.GetSessionPresence(sessionId));

    public async Task<SessionSnapshotEnvelope> RequestSnapshot(Guid sessionId)
    {
        var cancellationToken = Context.ConnectionAborted;
        var snapshot = await sessionSnapshotHydrator.GetOrHydrateAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"No snapshot found for session '{sessionId}'.");
        AttachPresence(sessionId, snapshot);
        return snapshot;
    }

    public async Task<RecoverSessionResult> RecoverSession(Guid sessionId, int lastKnownVersion)
    {
        var cancellationToken = Context.ConnectionAborted;
        var replayDiffs = await sessionStateStore.GetDiffsAfterVersionAsync(sessionId, lastKnownVersion, cancellationToken);
        foreach (var diff in replayDiffs.OrderBy(x => x.DiffSequence))
        {
            await Clients.Caller.SendAsync("StateDiff", diff, cancellationToken);
        }

        if (replayDiffs.Count == 0)
        {
            var snapshot = await sessionSnapshotHydrator.GetOrHydrateAsync(sessionId, cancellationToken)
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

    private string ResolveActor(string? guestActorId = null)
        => Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub")
            ?? (string.IsNullOrWhiteSpace(guestActorId) ? null : guestActorId.Trim())
            ?? "unknown";

    private string ResolveDisplayName(string? displayName = null, string? guestActorId = null)
        => Context.User?.FindFirstValue(ClaimTypes.Name)
            ?? Context.User?.FindFirstValue(ClaimTypes.Email)
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? (string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim())
            ?? ResolveActor(guestActorId);

    private string ResolveActorRole()
    {
        var roles = Context.User?.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList() ?? [];
        if (roles.Any(role => role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return "admin";
        }

        if (roles.Any(role => role.Equals("GM", StringComparison.OrdinalIgnoreCase)))
        {
            return "gm";
        }

        return "player";
    }

    private string ResolvePolicyScope(PlayerActionEnvelope action)
    {
        if (action.ActionType.Trim().StartsWith("gm.", StringComparison.OrdinalIgnoreCase))
        {
            return "gm";
        }

        var role = ResolveActorRole();
        return role is "gm" or "admin" ? "gm" : "player";
    }

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
        snapshot.PlayerPresence = (playerPresenceTracker.GetSessionPresence(sessionId) ?? [])
            .ToList();
    }

    private static string SessionGroup(Guid sessionId) => $"session:{sessionId}";
}
