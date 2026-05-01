using System.Text.Json.Nodes;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.BuiltIns;
using EscapeRoom.TriggerEngine.Evaluation;
using EscapeRoom.TriggerEngine.Idempotency;
using EscapeRoom.TriggerEngine.Registry;
using FluentAssertions;

namespace EscapeRoom.Tests.Unit.TriggerEngine.Evaluation;

public class TriggerGraphEvaluatorTests
{
    private readonly TriggerGraphEvaluator _evaluator;

    public TriggerGraphEvaluatorTests()
    {
        _evaluator = new TriggerGraphEvaluator(
            new ConditionRegistry(new ActionTypeConditionEvaluator()),
            new CombinatorRegistry(new AllTrueCombinatorEvaluator(), new AnyTrueCombinatorEvaluator()),
            new EffectRegistry(new EmitMessageEffectExecutor(), new SetStateValueEffectExecutor()),
            new NoopIdempotencyStore(),
            new IdempotencyKeyBuilder());
    }

    [Fact]
    public async Task EvaluateAsync_ShouldApplyEffectWhenConditionMatches()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new()
                {
                    NodeId = "cond",
                    Family = "condition",
                    Type = "actionTypeEquals",
                    Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                },
                new()
                {
                    NodeId = "effect",
                    Family = "effect",
                    Type = "setStateValue",
                    Config = new Dictionary<string, object?> { ["key"] = "doorUnlocked", ["value"] = "true" }
                }
            ],
            Edges = [new() { FromNodeId = "cond", ToNodeId = "effect" }]
        };

        var result = await _evaluator.EvaluateAsync(
            graph,
            new EvaluationContext
            {
                SessionId = Guid.NewGuid(),
                RoomId = Guid.NewGuid(),
                Action = new PlayerActionEnvelope { ActionType = "inspect", ClientActionId = "client-1" },
                State = new JsonObject()
            });

        result.AppliedEffects.Should().Contain("effect");
        result.UpdatedStateJson.Should().Contain("doorUnlocked");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldNotApplyEffectWhenConditionDoesNotMatch()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new()
                {
                    NodeId = "cond",
                    Family = "condition",
                    Type = "actionTypeEquals",
                    Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                },
                new()
                {
                    NodeId = "effect",
                    Family = "effect",
                    Type = "setStateValue",
                    Config = new Dictionary<string, object?> { ["key"] = "doorUnlocked", ["value"] = "true" }
                }
            ],
            Edges = [new() { FromNodeId = "cond", ToNodeId = "effect" }]
        };

        var result = await _evaluator.EvaluateAsync(
            graph,
            new EvaluationContext
            {
                SessionId = Guid.NewGuid(),
                RoomId = Guid.NewGuid(),
                Action = new PlayerActionEnvelope { ActionType = "inspect", ClientActionId = "client-1" },
                State = new JsonObject()
            });

        result.AppliedEffects.Should().NotContain("effect");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldApplyEffectWithNoConditions()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new()
                {
                    NodeId = "effect",
                    Family = "effect",
                    Type = "setStateValue",
                    Config = new Dictionary<string, object?> { ["key"] = "doorUnlocked", ["value"] = "true" }
                }
            ],
            Edges = []
        };

        var result = await _evaluator.EvaluateAsync(
            graph,
            new EvaluationContext
            {
                SessionId = Guid.NewGuid(),
                RoomId = Guid.NewGuid(),
                Action = new PlayerActionEnvelope { ActionType = "inspect", ClientActionId = "client-1" },
                State = new JsonObject()
            });

        result.AppliedEffects.Should().Contain("effect");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldUseAllTrueCombinator()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new()
                {
                    NodeId = "cond1",
                    Family = "condition",
                    Type = "actionTypeEquals",
                    Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                },
                new()
                {
                    NodeId = "cond2",
                    Family = "condition",
                    Type = "actionTypeEquals",
                    Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                },
                new()
                {
                    NodeId = "comb",
                    Family = "combinator",
                    Type = "allTrue"
                },
                new()
                {
                    NodeId = "effect",
                    Family = "effect",
                    Type = "setStateValue",
                    Config = new Dictionary<string, object?> { ["key"] = "doorUnlocked", ["value"] = "true" }
                }
            ],
            Edges =
            [
                new() { FromNodeId = "cond1", ToNodeId = "comb" },
                new() { FromNodeId = "cond2", ToNodeId = "comb" },
                new() { FromNodeId = "comb", ToNodeId = "effect" }
            ]
        };

        var result = await _evaluator.EvaluateAsync(
            graph,
            new EvaluationContext
            {
                SessionId = Guid.NewGuid(),
                RoomId = Guid.NewGuid(),
                Action = new PlayerActionEnvelope { ActionType = "inspect", ClientActionId = "client-1" },
                State = new JsonObject()
            });

        result.AppliedEffects.Should().Contain("effect");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldUseAnyTrueCombinator()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new()
                {
                    NodeId = "cond1",
                    Family = "condition",
                    Type = "actionTypeEquals",
                    Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                },
                new()
                {
                    NodeId = "cond2",
                    Family = "condition",
                    Type = "actionTypeEquals",
                    Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                },
                new()
                {
                    NodeId = "comb",
                    Family = "combinator",
                    Type = "anyTrue"
                },
                new()
                {
                    NodeId = "effect",
                    Family = "effect",
                    Type = "setStateValue",
                    Config = new Dictionary<string, object?> { ["key"] = "doorUnlocked", ["value"] = "true" }
                }
            ],
            Edges =
            [
                new() { FromNodeId = "cond1", ToNodeId = "comb" },
                new() { FromNodeId = "cond2", ToNodeId = "comb" },
                new() { FromNodeId = "comb", ToNodeId = "effect" }
            ]
        };

        var result = await _evaluator.EvaluateAsync(
            graph,
            new EvaluationContext
            {
                SessionId = Guid.NewGuid(),
                RoomId = Guid.NewGuid(),
                Action = new PlayerActionEnvelope { ActionType = "inspect", ClientActionId = "client-1" },
                State = new JsonObject()
            });

        result.AppliedEffects.Should().Contain("effect");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldEmitMessage()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new()
                {
                    NodeId = "effect",
                    Family = "effect",
                    Type = "emitMessage",
                    Config = new Dictionary<string, object?> { ["message"] = "Hello World" }
                }
            ],
            Edges = []
        };

        var result = await _evaluator.EvaluateAsync(
            graph,
            new EvaluationContext
            {
                SessionId = Guid.NewGuid(),
                RoomId = Guid.NewGuid(),
                Action = new PlayerActionEnvelope { ActionType = "inspect", ClientActionId = "client-1" },
                State = new JsonObject()
            });

        result.AppliedEffects.Should().Contain("effect");
        result.EmittedMessages.Should().ContainSingle();
        result.EmittedMessages[0].Should().Contain("Hello World");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldTrackChangedEntities()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new()
                {
                    NodeId = "effect",
                    Family = "effect",
                    Type = "setStateValue",
                    Config = new Dictionary<string, object?> { ["key"] = "doorUnlocked", ["value"] = "true" }
                }
            ],
            Edges = []
        };

        var result = await _evaluator.EvaluateAsync(
            graph,
            new EvaluationContext
            {
                SessionId = Guid.NewGuid(),
                RoomId = Guid.NewGuid(),
                Action = new PlayerActionEnvelope { ActionType = "inspect", ClientActionId = "client-1" },
                State = new JsonObject()
            });

        result.ChangedEntities.Should().Contain("state");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldThrowOnUnsupportedNodeFamily()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new()
                {
                    NodeId = "bad",
                    Family = "invalid",
                    Type = "something"
                }
            ],
            Edges = []
        };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _evaluator.EvaluateAsync(
                graph,
                new EvaluationContext
                {
                    SessionId = Guid.NewGuid(),
                    RoomId = Guid.NewGuid(),
                    Action = new PlayerActionEnvelope { ActionType = "inspect", ClientActionId = "client-1" },
                    State = new JsonObject()
                }));
    }
}