using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Evaluation;
using FluentAssertions;

namespace EscapeRoom.Tests.Unit.TriggerEngine.Evaluation;

public class TopologicalSorterTests
{
    [Fact]
    public void Sort_ShouldKeepDependenciesBeforeDependents()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "cond", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "comb", Family = "combinator", Type = "allTrue" },
                new() { NodeId = "eff", Family = "effect", Type = "emitMessage" }
            ],
            Edges =
            [
                new() { FromNodeId = "cond", ToNodeId = "comb" },
                new() { FromNodeId = "comb", ToNodeId = "eff" }
            ]
        };

        var sorted = TopologicalSorter.Sort(graph);
        sorted.IndexOf("cond").Should().BeLessThan(sorted.IndexOf("comb"));
        sorted.IndexOf("comb").Should().BeLessThan(sorted.IndexOf("eff"));
    }

    [Fact]
    public void Sort_ShouldHandleMultipleRoots()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "root1", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "root2", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "child", Family = "effect", Type = "emitMessage" }
            ],
            Edges =
            [
                new() { FromNodeId = "root1", ToNodeId = "child" },
                new() { FromNodeId = "root2", ToNodeId = "child" }
            ]
        };

        var sorted = TopologicalSorter.Sort(graph);
        sorted.Should().ContainInOrder("root1", "child");
        sorted.Should().ContainInOrder("root2", "child");
    }

    [Fact]
    public void Sort_ShouldHandleDiamondDependency()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "a", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "b", Family = "combinator", Type = "allTrue" },
                new() { NodeId = "c", Family = "combinator", Type = "anyTrue" },
                new() { NodeId = "d", Family = "effect", Type = "emitMessage" }
            ],
            Edges =
            [
                new() { FromNodeId = "a", ToNodeId = "b" },
                new() { FromNodeId = "a", ToNodeId = "c" },
                new() { FromNodeId = "b", ToNodeId = "d" },
                new() { FromNodeId = "c", ToNodeId = "d" }
            ]
        };

        var sorted = TopologicalSorter.Sort(graph);
        sorted.IndexOf("a").Should().BeLessThan(sorted.IndexOf("b"));
        sorted.IndexOf("a").Should().BeLessThan(sorted.IndexOf("c"));
        sorted.IndexOf("b").Should().BeLessThan(sorted.IndexOf("d"));
        sorted.IndexOf("c").Should().BeLessThan(sorted.IndexOf("d"));
    }

    [Fact]
    public void Sort_ShouldReturnSingleNode()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "only", Family = "effect", Type = "emitMessage" }
            ],
            Edges = []
        };

        var sorted = TopologicalSorter.Sort(graph);
        sorted.Should().ContainSingle();
        sorted[0].Should().Be("only");
    }

    [Fact]
    public void Sort_ShouldThrowOnCycle()
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

        Assert.Throws<InvalidOperationException>(() => TopologicalSorter.Sort(graph));
    }

    [Fact]
    public void Sort_ShouldHandleDeepChain()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "a", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "b", Family = "combinator", Type = "allTrue" },
                new() { NodeId = "c", Family = "combinator", Type = "anyTrue" },
                new() { NodeId = "d", Family = "combinator", Type = "allTrue" },
                new() { NodeId = "e", Family = "effect", Type = "emitMessage" }
            ],
            Edges =
            [
                new() { FromNodeId = "a", ToNodeId = "b" },
                new() { FromNodeId = "b", ToNodeId = "c" },
                new() { FromNodeId = "c", ToNodeId = "d" },
                new() { FromNodeId = "d", ToNodeId = "e" }
            ]
        };

        var sorted = TopologicalSorter.Sort(graph);
        sorted.Should().ContainInOrder("a", "b", "c", "d", "e");
    }

    [Fact]
    public void Sort_ShouldIgnoreCaseForNodeIds()
    {
        var graph = new TriggerGraphDefinition
        {
            Nodes =
            [
                new() { NodeId = "Parent", Family = "condition", Type = "actionTypeEquals" },
                new() { NodeId = "child", Family = "effect", Type = "emitMessage" }
            ],
            Edges =
            [
                new() { FromNodeId = "parent", ToNodeId = "Child" }
            ]
        };

        // Should not throw - case-insensitive matching
        var sorted = TopologicalSorter.Sort(graph);
        sorted.Should().ContainInOrder("Parent", "child");
    }
}