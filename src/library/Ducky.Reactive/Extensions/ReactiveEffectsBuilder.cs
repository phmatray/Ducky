// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Ducky.Reactive.Monitoring;

namespace Ducky.Reactive;

/// <summary>
/// Builder for configuring reactive effects.
/// </summary>
public sealed class ReactiveEffectsBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveEffectsBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public ReactiveEffectsBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <summary>
    /// Adds a reactive effect to the configuration.
    /// </summary>
    /// <typeparam name="TEffect">The type of reactive effect to add.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder Add<TEffect>()
        where TEffect : ReactiveEffect
    {
        _services.AddScoped<TEffect>();
        _services.AddScoped<ReactiveEffect>(sp => sp.GetRequiredService<TEffect>());
        return this;
    }

    /// <summary>
    /// Adds a reactive effect with a factory method.
    /// </summary>
    /// <typeparam name="TEffect">The type of reactive effect to add.</typeparam>
    /// <param name="factory">The factory method to create the effect.</param>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder Add<TEffect>(Func<IServiceProvider, TEffect> factory)
        where TEffect : ReactiveEffect
    {
        ArgumentNullException.ThrowIfNull(factory);

        _services.AddScoped(factory);
        _services.AddScoped<ReactiveEffect>(sp => sp.GetRequiredService<TEffect>());
        return this;
    }

    /// <summary>
    /// Adds a reactive effect with configuration.
    /// </summary>
    /// <typeparam name="TEffect">The type of reactive effect to add.</typeparam>
    /// <param name="configure">Action to configure the effect after creation.</param>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder Add<TEffect>(Action<TEffect> configure)
        where TEffect : ReactiveEffect, new()
    {
        ArgumentNullException.ThrowIfNull(configure);

        _services.AddScoped<TEffect>(sp =>
        {
            TEffect effect = ActivatorUtilities.CreateInstance<TEffect>(sp);
            configure(effect);
            return effect;
        });
        _services.AddScoped<ReactiveEffect>(sp => sp.GetRequiredService<TEffect>());
        return this;
    }

    /// <summary>
    /// Adds all reactive effects from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for reactive effects.</param>
    /// <param name="predicate">Optional predicate to filter which effects to register.</param>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder AddFromAssembly(
        Assembly assembly,
        Func<Type, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        IEnumerable<Type> effectTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.IsAssignableTo(typeof(ReactiveEffect)))
            .Where(predicate ?? (_ => true));

        foreach (Type effectType in effectTypes)
        {
            _services.AddScoped(effectType);
            _services.AddScoped<ReactiveEffect>(sp => (ReactiveEffect)sp.GetRequiredService(effectType));
        }

        return this;
    }

    /// <summary>
    /// Adds reactive effects from the calling assembly.
    /// </summary>
    /// <param name="predicate">Optional predicate to filter which effects to register.</param>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder AddFromCallingAssembly(Func<Type, bool>? predicate = null)
    {
        return AddFromAssembly(Assembly.GetCallingAssembly(), predicate);
    }

    /// <summary>
    /// Adds reactive effects that match a specific namespace pattern.
    /// </summary>
    /// <param name="namespacePattern">The namespace pattern to match (e.g., "MyApp.Effects").</param>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder AddFromNamespace(string namespacePattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespacePattern);

        return AddFromCallingAssembly(type =>
            type.Namespace?.StartsWith(namespacePattern, StringComparison.Ordinal) ?? false);
    }

    /// <summary>
    /// Configures options for all reactive effects.
    /// </summary>
    /// <param name="configure">Action to configure the options.</param>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder WithOptions(Action<ReactiveEffectOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Adds a custom error handler for reactive effects.
    /// </summary>
    /// <typeparam name="TErrorHandler">The type of error handler.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder WithErrorHandler<TErrorHandler>()
        where TErrorHandler : class, IReactiveEffectErrorHandler
    {
        _services.AddScoped<IReactiveEffectErrorHandler, TErrorHandler>();
        return this;
    }

    /// <summary>
    /// Adds a monitor for reactive effects diagnostics.
    /// </summary>
    /// <typeparam name="TMonitor">The type of monitor.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder WithMonitor<TMonitor>()
        where TMonitor : class, IReactiveEffectMonitor
    {
        _services.AddSingleton<IReactiveEffectMonitor, TMonitor>();
        return this;
    }

    /// <summary>
    /// Adds the default logging monitor.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder WithLoggingMonitor()
    {
        return WithMonitor<LoggingEffectMonitor>();
    }

    /// <summary>
    /// Adds the metrics monitor for performance tracking.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public ReactiveEffectsBuilder WithMetricsMonitor()
    {
        _services.AddSingleton<MetricsEffectMonitor>();
        _services.AddSingleton<IReactiveEffectMonitor>(sp => sp.GetRequiredService<MetricsEffectMonitor>());
        return this;
    }
}
