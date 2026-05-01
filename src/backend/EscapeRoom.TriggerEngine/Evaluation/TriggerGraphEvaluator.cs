using System.Text.Json;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Idempotency;
using EscapeRoom.TriggerEngine.Registry;

namespace EscapeRoom.TriggerEngine.Evaluation;

public class TriggerGraphEvaluator(
    IConditionRegistry conditionRegistry,
    ICombinatorRegistry combinatorRegistry,
    IEffectRegistry effectRegistry,
    IIdempotencyStore idempotencyStore,
    IdempotencyKeyBuilder idempotencyKeyBuilder) : ITriggerGraphEvaluator
{
    public async Task<EvaluationResult> EvaluateAsync(
        TriggerGraphDefinition graph,
        EvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new EvaluationResult();
        var orderedNodeIds = TopologicalSorter.Sort(graph);
        var nodes = graph.Nodes.ToDictionary(x => x.NodeId, StringComparer.OrdinalIgnoreCase);
        var incoming = graph.Nodes.ToDictionary(x => x.NodeId, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Edges)
        {
            incoming[edge.ToNodeId].Add(edge.FromNodeId);
        }

        var nodeTruth = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var nodeId in orderedNodeIds)
        {
            var node = nodes[nodeId];
            var inputs = incoming[nodeId].Select(x => nodeTruth.GetValueOrDefault(x)).ToList();
            var family = node.Family.Trim().ToLowerInvariant();
            switch (family)
            {
                case "condition":
                    nodeTruth[nodeId] = conditionRegistry.Get(node.Type).Evaluate(context, node);
                    break;
                case "combinator":
                    nodeTruth[nodeId] = combinatorRegistry.Get(node.Type).Evaluate(context, node, inputs);
                    break;
                case "effect":
                    var shouldRun = inputs.Count == 0 || inputs.All(x => x);
                    if (!shouldRun)
                    {
                        nodeTruth[nodeId] = false;
                        break;
                    }

                    var key = idempotencyKeyBuilder.Build(context.SessionId, node, context.Action);
                    if (await idempotencyStore.ExistsAsync(key, cancellationToken))
                    {
                        nodeTruth[nodeId] = true;
                        break;
                    }

                    var executionResult = effectRegistry.Get(node.Type).Execute(context, node);
                    await idempotencyStore.MarkAsync(key, idempotencyKeyBuilder.ResolveTtl(node), cancellationToken);
                    nodeTruth[nodeId] = executionResult.Applied;
                    if (executionResult.Applied)
                    {
                        result.AppliedEffects.Add(node.NodeId);
                        result.ChangedEntities.AddRange(executionResult.ChangedEntities);
                        result.EmittedMessages.AddRange(executionResult.Messages);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported node family '{node.Family}'.");
            }
        }

        result.UpdatedStateJson = context.State.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return result;
    }
}
