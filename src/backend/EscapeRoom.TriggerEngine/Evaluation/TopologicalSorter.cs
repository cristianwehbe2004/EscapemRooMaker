using EscapeRoom.Application.Triggering.Contracts;

namespace EscapeRoom.TriggerEngine.Evaluation;

public static class TopologicalSorter
{
    public static List<string> Sort(TriggerGraphDefinition graph)
    {
        var indegree = graph.Nodes.ToDictionary(x => x.NodeId, _ => 0, StringComparer.OrdinalIgnoreCase);
        var adjacency = graph.Nodes.ToDictionary(x => x.NodeId, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var edge in graph.Edges)
        {
            // Find actual node id using case-insensitive lookup
            var fromId = adjacency.Keys.First(k => string.Equals(k, edge.FromNodeId, StringComparison.OrdinalIgnoreCase));
            var toId = adjacency.Keys.First(k => string.Equals(k, edge.ToNodeId, StringComparison.OrdinalIgnoreCase));
            
            adjacency[fromId].Add(toId);
            indegree[toId]++;
        }

        var queue = new Queue<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var result = new List<string>();
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);

            foreach (var child in adjacency[node])
            {
                indegree[child]--;
                if (indegree[child] == 0)
                {
                    queue.Enqueue(child);
                }
            }
        }

        if (result.Count != graph.Nodes.Count)
        {
            throw new InvalidOperationException("Cannot topologically sort cyclic graph.");
        }

        return result;
    }
}
