// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Ducky.Builder;
using Ducky.Reactive.Patterns;
using System.Reactive.Linq;
using Ducky.Reactive.Monitoring;
using Ducky.Reactive.Middlewares.ReactiveEffects;

namespace Ducky.Reactive.Tests;

/// <summary>
/// Simple examples demonstrating the improved Ducky.Reactive APIs.
/// </summary>
public class SimpleApiExampleTests
{
    /// <summary>
    /// Example showing fluent API for registering reactive effects.
    /// </summary>
    [Fact]
    public void FluentApi_ConfigureReactiveEffects_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging();

        // Act - Configure reactive effects first
        services.AddReactiveEffects(effects =>
        {
            effects
                .Add<SimpleLogEffect>()
                .WithOptions(options =>
                {
                    options.EnableDiagnostics = true;
                    options.LogLifecycleEvents = true;
                })
                .WithLoggingMonitor();
        });

        // Then configure Ducky store
        services.AddDuckyStore(builder =>
        {
            builder
                .UseDefaultMiddlewares()
                .AddMiddleware<ReactiveEffectMiddleware>();
        });

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        IStore? store = provider.GetService<IStore>();
        store.ShouldNotBeNull();
    }

    /// <summary>
    /// Example showing assembly scanning for automatic effect registration.
    /// </summary>
    [Fact]
    public void AssemblyScanning_ShouldRegisterAllEffects()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - Configure effects and store
        services.AddReactiveEffects(effects =>
        {
            effects.AddFromAssembly(
                typeof(SimpleApiExampleTests).Assembly,
                type => type.Namespace == "Ducky.Reactive.Tests" &&
                        type.Name.EndsWith("Effect"));
        });

        services.AddDuckyStore(builder =>
        {
            builder
                .UseDefaultMiddlewares()
                .AddMiddleware<ReactiveEffectMiddleware>();
        });

        // Assert - Effects should be registered
        ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<ReactiveEffect> registeredEffects = provider.GetServices<ReactiveEffect>();
        registeredEffects.Count().ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Example showing enhanced base classes for effects.
    /// </summary>
    [Fact]
    public void EnhancedBaseClass_ShouldProvideLifecycleHooks()
    {
        // Arrange
        LifecycleAwareEffect effect = new();
        IObservable<object> actions = Observable.Never<object>();
        IObservable<IRootState> states = Observable.Never<IRootState>();

        // Act
        IObservable<object> result = effect.Handle(actions, states);
        IDisposable subscription = result.Subscribe();

        // Assert
        effect.IsInitialized.ShouldBeTrue();

        // Cleanup
        effect.Dispose();
        effect.IsDisposed.ShouldBeTrue();
    }

    /// <summary>
    /// Example showing pattern-based effects.
    /// </summary>
    [Fact]
    public void PatternBasedEffects_ShouldSimplifyCommonScenarios()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - Configure pattern-based effects
        services.AddReactiveEffects(effects =>
        {
            effects
                .Add<SimpleDebounceEffect>()
                .Add<SimplePollingEffect>()
                .WithMetricsMonitor();
        });

        services.AddDuckyStore(builder =>
        {
            builder
                .UseDefaultMiddlewares()
                .AddMiddleware<ReactiveEffectMiddleware>();
        });

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();

        // Verify metrics monitor is registered
        MetricsEffectMonitor? monitor = provider.GetService<MetricsEffectMonitor>();
        monitor.ShouldNotBeNull();
    }
}

// Example effect implementations
public class SimpleLogEffect : ReactiveEffectBase
{
    protected override IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IRootState> rootState)
    {
        return actions
            .Where(action => action is LogAction)
            .Select(action => new LogProcessed(DateTime.UtcNow));
    }
}

public class LifecycleAwareEffect : ReactiveEffectBase
{
    protected override Task OnInitializeAsync()
    {
        // Initialization logic here
        return Task.CompletedTask;
    }

    protected override Task OnDisposeAsync()
    {
        // Cleanup logic here
        return Task.CompletedTask;
    }

    protected override IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IRootState> rootState)
    {
        return Observable.Empty<object>();
    }
}

public class SimpleDebounceEffect : DebouncedEffect<SearchAction>
{
    public SimpleDebounceEffect()
        : base(TimeSpan.FromMilliseconds(300))
    {
    }

    protected override IObservable<object> ProcessDebouncedAction(
        SearchAction action,
        IObservable<IRootState> rootState)
    {
        return Observable.Return(new SearchProcessed(action.Query));
    }
}

public class SimplePollingEffect : PollingEffect
{
    public SimplePollingEffect()
        : base(TimeSpan.FromSeconds(5))
    {
        StartImmediately = false;
    }

    protected override IObservable<object> GetStopSignal(IObservable<object> actions)
    {
        return actions.Where(a => a is StopPollingAction);
    }

    protected override IObservable<object> Poll(IRootState rootState)
    {
        return Observable.Return(new PollCompleted(DateTime.UtcNow));
    }
}

// Test actions
public record LogAction(string Message);
public record LogProcessed(DateTime Timestamp);
public record SearchAction(string Query);
public record SearchProcessed(string Query);
public record StopPollingAction;
public record PollCompleted(DateTime Timestamp);