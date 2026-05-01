namespace EscapeRoom.TriggerEngine.Evaluation;

public class EvaluationResult
{
    public string UpdatedStateJson { get; set; } = "{}";
    public List<string> AppliedEffects { get; } = new();
    public List<string> ChangedEntities { get; } = new();
    public List<string> EmittedMessages { get; } = new();
}
