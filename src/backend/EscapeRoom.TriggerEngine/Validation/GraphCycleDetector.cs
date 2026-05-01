namespace EscapeRoom.TriggerEngine.Validation;

public static class GraphCycleDetector
{
    public static bool HasCycle(IReadOnlyDictionary<string, List<string>> adjacency)
    {
        var indegree = adjacency.Keys.ToDictionary(k => k, _ => 0);
        foreach (var (_, children) in adjacency)
        {
            foreach (var child in children)
            {
                if (indegree.ContainsKey(child))
                {
                    indegree[child]++;
                }
            }
        }

        var queue = new Queue<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var visited = 0;
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            visited++;
            foreach (var child in adjacency[node])
            {
                indegree[child]--;
                if (indegree[child] == 0)
                {
                    queue.Enqueue(child);
                }
            }
        }

        return visited != adjacency.Count;
    }
}
