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
            new ConditionRegistry(
                new ActionTypeConditionEvaluator(),
                new TargetEqualsConditionEvaluator(),
                new InventoryHasItemConditionEvaluator(),
                new StateValueEqualsConditionEvaluator(),
                new PayloadValueEqualsConditionEvaluator()),
            new CombinatorRegistry(new AllTrueCombinatorEvaluator(), new AnyTrueCombinatorEvaluator()),
            new EffectRegistry(
                new EmitMessageEffectExecutor(),
                new SetStateValueEffectExecutor(),
                new AddInventoryItemEffectExecutor(),
                new RemoveInventoryItemEffectExecutor(),
                new SetObjectStateEffectExecutor(),
                new CompleteSessionEffectExecutor(),
                new EmitClueEffectExecutor(),
                new TransitionRoomEffectExecutor()),
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
    public async Task EvaluateAsync_ShouldTransitionRoomAndPreserveInventory()
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
                    Type = "transitionRoom",
                    Config = new Dictionary<string, object?>
                    {
                        ["room"] = new Dictionary<string, object?>
                        {
                            ["roomName"] = "Inner Vault",
                            ["themeId"] = "artdeco",
                            ["width"] = 800,
                            ["height"] = 500,
                            ["backgroundColor"] = "#111827",
                            ["assets"] = Array.Empty<object>(),
                            ["layers"] = Array.Empty<object>(),
                            ["hotspots"] = Array.Empty<object>(),
                            ["objectStates"] = Array.Empty<object>()
                        }
                    }
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
                Action = new PlayerActionEnvelope { ActionType = "inspect", ClientActionId = "client-2" },
                State = JsonNode.Parse("""
                    {
                      "room": {
                        "roomName": "Outer Office"
                      },
                      "inventory": [
                        { "id": "vault-key", "label": "Vault Key", "quantity": 1 }
                      ],
                      "session": {
                        "roomName": "Outer Office"
                      }
                    }
                    """)!.AsObject()
            });

        result.ChangedEntities.Should().Contain("room.transition");
        result.ChangedEntities.Should().Contain("room");
        result.ChangedEntities.Should().Contain("session");
        result.UpdatedStateJson.Should().Contain("Inner Vault");
        result.UpdatedStateJson.Should().Contain("vault-key");
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
