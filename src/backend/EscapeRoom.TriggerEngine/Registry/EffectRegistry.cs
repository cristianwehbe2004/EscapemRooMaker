using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.BuiltIns;

namespace EscapeRoom.TriggerEngine.Registry;

public class EffectRegistry(
    EmitMessageEffectExecutor emitMessageEffectExecutor,
    SetStateValueEffectExecutor setStateValueEffectExecutor) : IEffectRegistry
{
    private readonly Dictionary<string, IEffectExecutor> _executors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["emitMessage"] = emitMessageEffectExecutor,
        ["setStateValue"] = setStateValueEffectExecutor
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
