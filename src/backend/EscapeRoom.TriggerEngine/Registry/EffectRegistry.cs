using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.BuiltIns;

namespace EscapeRoom.TriggerEngine.Registry;

public class EffectRegistry(
    EmitMessageEffectExecutor emitMessageEffectExecutor,
    SetStateValueEffectExecutor setStateValueEffectExecutor,
    AddInventoryItemEffectExecutor addInventoryItemEffectExecutor,
    RemoveInventoryItemEffectExecutor removeInventoryItemEffectExecutor,
    SetObjectStateEffectExecutor setObjectStateEffectExecutor,
    CompleteSessionEffectExecutor completeSessionEffectExecutor,
    EmitClueEffectExecutor emitClueEffectExecutor,
    TransitionRoomEffectExecutor transitionRoomEffectExecutor) : IEffectRegistry
{
    private readonly Dictionary<string, IEffectExecutor> _executors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["emitMessage"] = emitMessageEffectExecutor,
        ["setStateValue"] = setStateValueEffectExecutor,
        ["addInventoryItem"] = addInventoryItemEffectExecutor,
        ["removeInventoryItem"] = removeInventoryItemEffectExecutor,
        ["setObjectState"] = setObjectStateEffectExecutor,
        ["completeSession"] = completeSessionEffectExecutor,
        ["emitClue"] = emitClueEffectExecutor,
        ["transitionRoom"] = transitionRoomEffectExecutor
    };

    public IEffectExecutor Get(string type)
    {
        if (_executors.TryGetValue(type, out var executor))
        {
            return executor;
        }

        throw new InvalidOperationException($"Effect executor '{type}' is not registered.");
    }
}
