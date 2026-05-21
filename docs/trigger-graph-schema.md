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
| `targetEquals` | `expectedTarget` or `targetId` | Matches when the player action target matches a hotspot/object id |
| `inventoryHasItem` | `itemId` | Matches when the session inventory contains an item id |
| `stateValueEquals` | `key` or `path`, `value` | Matches when a dot-path value in session state equals the configured value |
| `payloadValueEquals` | `key`, `value` | Matches when action payload contains a specific key/value pair (for example `itemId`) |

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
| `addInventoryItem` | `item` or `id`/`label`/`type` | Adds an item to inventory if not already present |
| `removeInventoryItem` | `itemId` or `id` | Removes an item from inventory |
| `setObjectState` | `objectId`, `visible`, `available`, `locked`, `interactive` | Updates room object state used by hotspots/layers/assets |
| `emitClue` | `clue` or `message` | Adds a player-facing clue and emits it as a message |
| `transitionRoom` | `room`, optional `message` | Replaces the active room subtree in-session while preserving timer, inventory, and session continuity |
| `completeSession` | `message` | Marks the session completed and emits an optional completion message |

## Player Actions

The player UI submits these action types:

| Action | Target | Payload |
|--------|--------|---------|
| `inspect` | hotspot/object id | `{}` |
| `pickup` | hotspot/object id | `{}` |
| `inventory.use` | target hotspot/object id | `{ "itemId": "inventory-item-id" }` |
| `inventory.combine` | secondary item id | `{ "primaryItemId": "...", "secondaryItemId": "..." }` |

## Session Locking
- Redis distributed lock ensures only one evaluation runs per session at a time
- Lock timeout: 30 seconds
- Automatic release after evaluation completes
- Prevents race conditions from concurrent player actions
