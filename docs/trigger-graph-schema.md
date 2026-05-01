# Trigger Graph Schema

## Overview
This schema defines the structure of directed acyclic graphs (DAGs) used for trigger evaluation in the Escape Room system. Each room contains a trigger graph that defines how player actions interact with room state and trigger effects.

## Core Types

### TriggerGraphDefinition
Root container for the entire graph.

```json
{
  "version": 1,
  "metadata": {
    "roomId": "uuid",
    "name": "Room Graph",
    "lastModified": "2026-04-25T00:00:00Z"
  },
  "nodes": [ /* TriggerNodeDefinition array */ ],
  "edges": [ /* TriggerEdgeDefinition array */ ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `version` | integer | Schema version (currently 1) |
| `metadata` | `Dictionary<string, string>` | Arbitrary key-value metadata |
| `nodes` | `TriggerNodeDefinition[]` | All nodes in the graph |
| `edges` | `TriggerEdgeDefinition[]` | Connections between nodes |

### TriggerNodeDefinition
Single node in the graph representing a condition, combinator, or effect.

```json
{
  "nodeId": "unique-node-id",
  "family": "condition | combinator | effect",
  "type": "condition-type-or-effect-type",
  "config": { /* node-specific configuration */ },
  "policy": { /* EffectPolicyDefinition */ }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `nodeId` | string | Unique identifier for the node (case-insensitive) |
| `family` | string | Node family: `condition`, `combinator`, or `effect` |
| `type` | string | Specific type within the family |
| `config` | `Dictionary<string, object>` | Node configuration parameters |
| `policy` | `EffectPolicyDefinition` | Execution policy (effects only) |

### TriggerEdgeDefinition
Directed edge connecting two nodes.

```json
{
  "fromNodeId": "source-node-id",
  "toNodeId": "target-node-id"
}
```

### EffectPolicyDefinition
Execution policy for effect nodes.

```json
{
  "mode": "one-shot | repeatable",
  "keyWindowSeconds": 30
}
```

| Field | Type | Description |
|-------|------|-------------|
| `mode` | string | Idempotency mode: `one-shot` (default) or `repeatable` |
| `keyWindowSeconds` | integer | Window for repeatable effects (default: 30 seconds) |

## Execution Flow
1. **Validation**: Graph is validated to ensure it is a valid DAG with no cycles
2. **Topological Sort**: Nodes are ordered so dependencies are evaluated before dependents
3. **Evaluation**: Nodes are executed in topological order:
   - **Conditions**: Check if player action or state matches criteria
   - **Combinators**: Combine truth values from input nodes
   - **Effects**: Apply changes to state, emit messages, or trigger events

## Idempotency
- **One-shot mode**: Effect executes once per action/session combination, TTL 24 hours
- **Repeatable mode**: Effect may execute once per time window
- All idempotency keys are stored in Redis with appropriate TTLs

## Built-in Nodes

### Conditions
| Type | Config | Description |
|------|--------|-------------|
| `actionTypeEquals` | `expectedActionType` | Matches when action type equals the expected value |

### Combinators
| Type | Description |
|------|-------------|
| `allTrue` | True when all inputs are true |
| `anyTrue` | True when at least one input is true |

### Effects
| Type | Config | Description |
|------|--------|-------------|
| `setStateValue` | `key`, `value` | Sets a value in session state |
| `emitMessage` | `message` | Emits a message to all players in the session |

## Session Locking
- Redis distributed lock ensures only one evaluation runs per session at a time
- Lock timeout: 30 seconds
- Automatic release after evaluation completes
- Prevents race conditions from concurrent player actions