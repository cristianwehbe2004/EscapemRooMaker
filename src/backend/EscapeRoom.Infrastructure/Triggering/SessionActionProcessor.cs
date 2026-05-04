using System.Text.Json;
using System.Text.Json.Nodes;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Triggering;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Domain.Enums;
using EscapeRoom.Infrastructure.Data;
using EscapeRoom.Infrastructure.Sessions;
using EscapeRoom.TriggerEngine.Evaluation;
using EscapeRoom.TriggerEngine.Validation;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Triggering;

public class SessionActionProcessor(
    AppDbContext dbContext,
    ISessionLockService sessionLockService,
    ISessionStateStore sessionStateStore,
    ITriggerGraphValidator graphValidator,
    ITriggerGraphEvaluator graphEvaluator) : ISessionActionProcessor
{
    public async Task<StateDiffEnvelope> ProcessActionAsync(Guid sessionId, PlayerActionEnvelope action, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session '{sessionId}' was not found.");
        var room = await dbContext.Rooms.FirstOrDefaultAsync(x => x.Id == session.RoomId, cancellationToken)
            ?? throw new InvalidOperationException($"Room '{session.RoomId}' was not found.");

        var now = DateTime.UtcNow;
        if (session.Status == SessionStatus.Pending)
        {
            throw new InvalidOperationException("Session has not started yet.");
        }

        if (session.Status == SessionStatus.Active && session.EndsAtUtc is not null && session.EndsAtUtc <= now)
        {
            session.Status = SessionStatus.Expired;
            session.EndedAtUtc = now;
            session.LastActivityAtUtc = now;
            session.StateSnapshot = SessionStateFactory.WithSessionState(session.StateSnapshot, room, session, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Session timer has expired.");
        }

        if (session.Status is SessionStatus.Completed or SessionStatus.Cancelled or SessionStatus.Expired)
        {
            throw new InvalidOperationException($"Session is {session.Status}.");
        }

        var graph = ParseTriggerGraph(room.GraphDefinition);
        var validation = graphValidator.Validate(graph);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Trigger graph validation failed: {string.Join("; ", validation.Errors)}");
        }

        var lockHandle = await sessionLockService.AcquireAsync(sessionId, cancellationToken);
        try
        {
            var currentVersion = await dbContext.SessionSnapshots
                .Where(x => x.SessionId == sessionId)
                .OrderByDescending(x => x.Version)
                .Select(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

            var state = JsonNode.Parse(session.StateSnapshot) as JsonObject ?? new JsonObject();
            var evaluation = await graphEvaluator.EvaluateAsync(
                graph,
                new EvaluationContext
                {
                    SessionId = sessionId,
                    RoomId = room.Id,
                    Action = action,
                    State = state
                },
                cancellationToken);

            var nextSequence = (await dbContext.SessionEvents
                .Where(x => x.SessionId == sessionId)
                .OrderByDescending(x => x.SequenceNumber)
                .Select(x => (int?)x.SequenceNumber)
                .FirstOrDefaultAsync(cancellationToken) ?? 0) + 1;

            var nextVersion = currentVersion + 1;
            dbContext.SessionEvents.Add(new SessionEvent
            {
                SessionId = sessionId,
                SequenceNumber = nextSequence,
                EventType = action.ActionType,
                EventData = JsonSerializer.Serialize(action, JsonOptions()),
                OccurredAtUtc = DateTime.UtcNow
            });

            var stateAfterEvaluation = JsonNode.Parse(evaluation.UpdatedStateJson) as JsonObject ?? new JsonObject();
            if (StateMarksSessionCompleted(stateAfterEvaluation))
            {
                session.Status = SessionStatus.Completed;
                session.CompletedAtUtc ??= DateTime.UtcNow;
                session.EndedAtUtc ??= session.CompletedAtUtc;
            }

            session.LastActivityAtUtc = DateTime.UtcNow;
            session.StateSnapshot = SessionStateFactory.WithSessionState(
                stateAfterEvaluation.ToJsonString(JsonOptions()),
                room,
                session,
                DateTime.UtcNow);

            dbContext.SessionSnapshots.Add(new SessionSnapshot
            {
                SessionId = sessionId,
                Version = nextVersion,
                StateData = session.StateSnapshot,
                CreatedAtUtc = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            var changedEntities = evaluation.ChangedEntities.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var emittedMessages = evaluation.EmittedMessages.ToList();
            var appliedEffects = evaluation.AppliedEffects.ToList();
            ApplyBuiltInGmEffects(action, changedEntities, emittedMessages, appliedEffects);
            var (statePatch, fullStateJson) = StateDiffPayloadBuilder.Build(
                session.StateSnapshot,
                action.ActionType,
                changedEntities);

            var diff = new StateDiffEnvelope
            {
                SessionVersion = nextVersion,
                CorrelationId = action.ClientActionId,
                EmittedAtUtc = DateTime.UtcNow,
                ChangedEntities = changedEntities,
                EmittedMessages = emittedMessages,
                AppliedEffects = appliedEffects,
                StatePatch = statePatch,
                FullStateJson = fullStateJson
            };

            diff.DiffSequence = await sessionStateStore.GetNextDiffSequenceAsync(sessionId, cancellationToken);
            await sessionStateStore.SaveSnapshotAsync(new SessionSnapshotEnvelope
            {
                SessionId = sessionId,
                SessionVersion = nextVersion,
                StateJson = session.StateSnapshot,
                ServerTimeUtc = DateTime.UtcNow
            }, cancellationToken);
            await sessionStateStore.AppendDiffAsync(sessionId, diff, cancellationToken);

            return diff;
        }
        finally
        {
            await sessionLockService.ReleaseAsync(lockHandle);
        }
    }

    private static bool StateMarksSessionCompleted(JsonObject state)
    {
        if (state["session"] is not JsonObject session)
        {
            return false;
        }

        var status = session["status"]?.GetValue<string>();
        return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyBuiltInGmEffects(
        PlayerActionEnvelope action,
        List<string> changedEntities,
        List<string> emittedMessages,
        List<string> appliedEffects)
    {
        var actionType = action.ActionType.Trim().ToLowerInvariant();
        switch (actionType)
        {
            case "gm.hint":
            {
                var hint = GetPayloadString(action.Payload, "hint");
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    emittedMessages.Add($"Hint: {hint}");
                }

                changedEntities.Add("gm.hints");
                appliedEffects.Add("gm.hint");
                break;
            }
            case "gm.broadcast":
            {
                var message = GetPayloadString(action.Payload, "message");
                if (!string.IsNullOrWhiteSpace(message))
                {
                    emittedMessages.Add($"GM: {message}");
                }

                changedEntities.Add("gm.broadcast");
                appliedEffects.Add("gm.broadcast");
                break;
            }
            case "gm.reveal":
            {
                var puzzleId = GetPayloadString(action.Payload, "puzzleId") ?? action.Target;
                if (!string.IsNullOrWhiteSpace(puzzleId))
                {
                    emittedMessages.Add($"GM revealed {puzzleId}.");
                    changedEntities.Add($"puzzle:{puzzleId}");
                }

                appliedEffects.Add("gm.reveal");
                break;
            }
            case "gm.force_sync":
                changedEntities.Add("session.sync");
                appliedEffects.Add("gm.force_sync");
                break;
        }
    }

    private static string? GetPayloadString(IReadOnlyDictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string s => s,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element => element.ToString(),
            _ => value.ToString()
        };
    }

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);

    private static TriggerGraphDefinition ParseTriggerGraph(string graphDefinitionJson)
    {
        if (string.IsNullOrWhiteSpace(graphDefinitionJson))
        {
            throw new InvalidOperationException("Room graph definition is empty.");
        }

        using var parsed = JsonDocument.Parse(graphDefinitionJson);
        var root = parsed.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("triggerGraph", out var triggerGraphElement))
        {
            var triggerGraph = triggerGraphElement.Deserialize<TriggerGraphDefinition>(JsonOptions());
            return triggerGraph ?? throw new InvalidOperationException("Room trigger graph is invalid JSON.");
        }

        var graph = JsonSerializer.Deserialize<TriggerGraphDefinition>(graphDefinitionJson, JsonOptions());
        return graph ?? throw new InvalidOperationException("Room graph definition is invalid JSON.");
    }
}
