using EscapeRoom.TriggerEngine.Abstractions;

namespace EscapeRoom.TriggerEngine.Registry;

public interface IEffectRegistry
{
    IEffectExecutor Get(string type);
}
