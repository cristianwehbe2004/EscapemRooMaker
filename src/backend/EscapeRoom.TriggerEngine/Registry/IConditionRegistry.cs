using EscapeRoom.TriggerEngine.Abstractions;

namespace EscapeRoom.TriggerEngine.Registry;

public interface IConditionRegistry
{
    IConditionEvaluator Get(string type);
}
