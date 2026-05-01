using EscapeRoom.Application.Triggering.Contracts;

namespace EscapeRoom.TriggerEngine.Validation;

public interface ITriggerGraphValidator
{
    ValidationResult Validate(TriggerGraphDefinition graph);
}
