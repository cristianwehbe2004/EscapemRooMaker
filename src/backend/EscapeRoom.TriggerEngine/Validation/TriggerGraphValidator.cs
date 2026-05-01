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

        var adjacency = graph.Nodes.ToDictionary(x => x.NodeId, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
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

            adjacency[edge.FromNodeId].Add(edge.ToNodeId);
        }

        if (result.Errors.Count == 0 && GraphCycleDetector.HasCycle(adjacency))
        {
            result.Errors.Add("Graph contains at least one cycle.");
        }

        return result;
    }
}
