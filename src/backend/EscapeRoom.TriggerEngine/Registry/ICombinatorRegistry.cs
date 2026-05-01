using EscapeRoom.TriggerEngine.Abstractions;

namespace EscapeRoom.TriggerEngine.Registry;

public interface ICombinatorRegistry
{
    ICombinatorEvaluator Get(string type);
}
