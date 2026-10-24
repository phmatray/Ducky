using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using R3;

namespace Ducky.Reactive;

/// <summary>Registration and operators for reactive effects.</summary>
public static class DuckyReactiveExtensions
{
    /// <summary>
    /// Registers a reactive effect. The store creates it from its scope when it materializes its effects, subscribes its
    /// pipeline on <see cref="StoreInitialized"/>, owns it and disposes it; its constructor dependencies are checked with
    /// the other configuration rules at the first store resolution (DUCKY309).
    /// </summary>
    /// <typeparam name="TEffect">The reactive effect type; it is not registered in DI.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static DuckyBuilder AddReactiveEffect<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TEffect>(this DuckyBuilder builder)
        where TEffect : ReactiveEffect
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Idempotent like AddEffect<T>: a generated registration plus a manual call create one pipeline. The core
        // de-duplicates the constructor check.
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ReactiveEffectFactory, ReactiveEffectFactory<TEffect>>());
        builder.Services.TryAddScoped<ReactiveBridge>();
        return builder
            .RequireResolvableConstructor(typeof(TEffect), "AddReactiveEffect<T>")
            .Use<ReactiveMiddleware>()
            .AddEffect<ReactiveHost>();
    }

    /// <summary>Filters the action stream to actions whose runtime type is exactly <typeparamref name="TAction"/>.</summary>
    /// <typeparam name="TAction">The action type; derived types are not matched.</typeparam>
    /// <param name="actions">The action stream.</param>
    /// <returns>The actions of exactly that type.</returns>
    public static Observable<TAction> OfActionType<TAction>(this Observable<object> actions)
        where TAction : notnull =>
        actions.Where(static action => action.GetType() == typeof(TAction)).Select(static action => (TAction)action);
}
