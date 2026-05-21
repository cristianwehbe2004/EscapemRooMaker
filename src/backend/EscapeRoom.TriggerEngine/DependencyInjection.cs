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
        services.AddScoped<ITriggerGraphEvaluator, TriggerGraphEvaluator>();
        services.AddSingleton<IdempotencyKeyBuilder>();
        services.AddSingleton<IIdempotencyStore, NoopIdempotencyStore>();

        services.AddSingleton<ActionTypeConditionEvaluator>();
        services.AddSingleton<TargetEqualsConditionEvaluator>();
        services.AddSingleton<InventoryHasItemConditionEvaluator>();
        services.AddSingleton<StateValueEqualsConditionEvaluator>();
        services.AddSingleton<PayloadValueEqualsConditionEvaluator>();
        services.AddSingleton<AllTrueCombinatorEvaluator>();
        services.AddSingleton<AnyTrueCombinatorEvaluator>();
        services.AddSingleton<EmitMessageEffectExecutor>();
        services.AddSingleton<SetStateValueEffectExecutor>();
        services.AddSingleton<AddInventoryItemEffectExecutor>();
        services.AddSingleton<RemoveInventoryItemEffectExecutor>();
        services.AddSingleton<SetObjectStateEffectExecutor>();
        services.AddSingleton<CompleteSessionEffectExecutor>();
        services.AddSingleton<EmitClueEffectExecutor>();
        services.AddSingleton<TransitionRoomEffectExecutor>();

        services.AddSingleton<IConditionRegistry, ConditionRegistry>();
        services.AddSingleton<ICombinatorRegistry, CombinatorRegistry>();
        services.AddSingleton<IEffectRegistry, EffectRegistry>();
        return services;
    }
}
