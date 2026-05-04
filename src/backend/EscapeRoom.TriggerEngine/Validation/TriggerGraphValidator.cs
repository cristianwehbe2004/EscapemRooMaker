using EscapeRoom.Application.Triggering.Contracts;

namespace EscapeRoom.TriggerEngine.Validation;

public class TriggerGraphValidator : ITriggerGraphValidator
{
    public ValidationResult Validate(TriggerGraphDefinition graph)
    {
        var result = new ValidationResult();
        if (graph.Nodes.Count == 0)
        {
            result.Errors.Add("Graph must contain at least one node.");
            return result;
        }

        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                result.Errors.Add("Node id cannot be empty.");
                continue;
            }

            if (!nodeIds.Add(node.NodeId))
            {
                result.Errors.Add($"Duplicate node id '{node.NodeId}'.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Family))
            {
                result.Errors.Add($"Node '{node.NodeId}' has empty family.");
            }

            if (string.IsNullOrWhiteSpace(node.Type))
            {
                result.Errors.Add($"Node '{node.NodeId}' has empty type.");
            }
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        var nodesById = graph.Nodes.ToDictionary(x => x.NodeId, StringComparer.OrdinalIgnoreCase);
        var adjacency = graph.Nodes.ToDictionary(x => x.NodeId, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        var incomingCounts = graph.Nodes.ToDictionary(x => x.NodeId, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Edges)
        {
            if (!adjacency.ContainsKey(edge.FromNodeId))
            {
                result.Errors.Add($"Edge source '{edge.FromNodeId}' does not exist.");
                continue;
            }

            if (!adjacency.ContainsKey(edge.ToNodeId))
            {
                result.Errors.Add($"Edge target '{edge.ToNodeId}' does not exist.");
                continue;
            }

            var fromFamily = NormalizeFamily(nodesById[edge.FromNodeId].Family);
            var toFamily = NormalizeFamily(nodesById[edge.ToNodeId].Family);
            if (!IsValidFamilyTransition(fromFamily, toFamily))
            {
                result.Errors.Add(
                    $"Invalid edge '{edge.FromNodeId}' ({fromFamily}) -> '{edge.ToNodeId}' ({toFamily}). Allowed transitions: condition->combinator|effect, combinator->combinator|effect.");
            }

            adjacency[edge.FromNodeId].Add(edge.ToNodeId);
            incomingCounts[edge.ToNodeId]++;
        }

        if (result.Errors.Count == 0 && GraphCycleDetector.HasCycle(adjacency))
        {
            result.Errors.Add("Graph contains at least one cycle.");
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        foreach (var node in graph.Nodes)
        {
            var family = NormalizeFamily(node.Family);
            if ((family == "condition" || family == "combinator") && incomingCounts[node.NodeId] == 0)
            {
                queue.Enqueue(node.NodeId);
                reachable.Add(node.NodeId);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in adjacency[current])
            {
                if (reachable.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var hasReachableEffect = graph.Nodes.Any(node =>
            NormalizeFamily(node.Family) == "effect" &&
            reachable.Contains(node.NodeId));

        if (!hasReachableEffect)
        {
            result.Errors.Add("Graph must include at least one effect node reachable from a condition/combinator path.");
        }

        return result;
    }

    private static string NormalizeFamily(string family) => family.Trim().ToLowerInvariant();

    private static bool IsValidFamilyTransition(string fromFamily, string toFamily)
    {
        return fromFamily switch
        {
            "condition" => toFamily is "combinator" or "effect",
            "combinator" => toFamily is "combinator" or "effect",
            _ => false
        };
    }
}
