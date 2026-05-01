using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Validation;
using FluentAssertions;

namespace EscapeRoom.Tests.Unit.TriggerEngine.Validation;

public class TriggerGraphValidatorTests
{
    private readonly TriggerGraphValidator _validator = new();

    [Fact]
    public void Validate_ShouldRejectGraphWithCycle()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "a", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "b", Family = "effect", Type = "emitMessage" }
            ],
            Edges =
            [
                new() { FromNodeId = "a", ToNodeId = "b" },
                new() { FromNodeId = "b", ToNodeId = "a" }
            ]
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ShouldRejectEmptyNodes()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes = [],
            Edges = []
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Graph must contain at least one node.");
    }

    [Fact]
    public void Validate_ShouldRejectDuplicateNodeIds()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "duplicate", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "duplicate", Family = "effect", Type = "emitMessage" }
            ],
            Edges = []
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ShouldRejectEmptyNodeId()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "", Family = "condition", Type = "actionTypeEquals" }
            ],
            Edges = []
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Node id cannot be empty.");
    }

    [Fact]
    public void Validate_ShouldRejectEmptyFamily()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "node1", Family = "", Type = "actionTypeEquals" }
            ],
            Edges = []
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Contains("family", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ShouldRejectEmptyType()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "node1", Family = "condition", Type = "" }
            ],
            Edges = []
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Contains("type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ShouldRejectEdgeToNonExistentSource()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "node1", Family = "condition", Type = "actionTypeEquals" }
            ],
            Edges =
            [
                new() { FromNodeId = "nonexistent", ToNodeId = "node1" }
            ]
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Contains("source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ShouldRejectEdgeToNonExistentTarget()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "node1", Family = "condition", Type = "actionTypeEquals" }
            ],
            Edges =
            [
                new() { FromNodeId = "node1", ToNodeId = "nonexistent" }
            ]
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Contains("target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ShouldAcceptValidGraph()
    {
        var graph = new TriggerGraphDefinition
        {
            Version = 1,
            Metadata = new Dictionary<string, string> { ["roomId"] = "test-room" },
            Nodes =
            [
                new() { NodeId = "cond1", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "effect1", Family = "effect", Type = "emitMessage" }
            ],
            Edges =
            [
                new() { FromNodeId = "cond1", ToNodeId = "effect1" }
            ]
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldIgnoreCaseForNodeIds()
    {
        // Test case-insensitive duplicate detection
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "Node1", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "node1", Family = "effect", Type = "emitMessage" }
            ],
            Edges = []
        };

        var result = _validator.Validate(graph);
        result.IsValid.Should().BeFalse();
    }
}