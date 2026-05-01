using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.BuiltIns;
using EscapeRoom.TriggerEngine.Evaluation;
using EscapeRoom.TriggerEngine.Idempotency;
using EscapeRoom.TriggerEngine.Registry;
using EscapeRoom.TriggerEngine.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace EscapeRoom.TriggerEngine;

public static class DependencyInjection
{
    public static IServiceCollection AddTriggerEngineCore(this IServiceCollection services)
    {
        services.AddSingleton<ITriggerGraphValidator, TriggerGraphValidator>();
        services.AddSingleton<ITriggerGraphEvaluator, TriggerGraphEvaluator>();
        services.AddSingleton<IdempotencyKeyBuilder>();
        services.AddSingleton<IIdempotencyStore, NoopIdempotencyStore>();

        services.AddSingleton<ActionTypeConditionEvaluator>();
        services.AddSingleton<AllTrueCombinatorEvaluator>();
        services.AddSingleton<AnyTrueCombinatorEvaluator>();
        services.AddSingleton<EmitMessageEffectExecutor>();
        services.AddSingleton<SetStateValueEffectExecutor>();

        services.AddSingleton<IConditionRegistry, ConditionRegistry>();
        services.AddSingleton<ICombinatorRegistry, CombinatorRegistry>();
        services.AddSingleton<IEffectRegistry, EffectRegistry>();
        return services;
    }
}
