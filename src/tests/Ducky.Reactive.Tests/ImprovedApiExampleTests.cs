// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Ducky.Reactive.Patterns;
using Ducky.Reactive.Testing;
using System.Collections.Immutable;

namespace Ducky.Reactive.Tests;

/// <summary>
/// Examples demonstrating the improved Ducky.Reactive APIs.
/// </summary>
public class ImprovedApiExampleTests
{
    /// <summary>
    /// Example 1: Simple effect registration with fluent API.
    /// </summary>
    [Fact]
    public void Example1_FluentApiRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - Configure reactive effects first
        services.AddReactiveEffects(effects =>
        {
            effects
                .Add<SimpleCounterEffect>()
                .Add<AdvancedCounterEffect>(effect => 
                {
                    // Configure the effect
                    effect.Threshold = 10;
                })
                .WithLoggingMonitor();
        });

        // Then configure Ducky store
        services.AddScoped<ISlice<TestCounterState>, TestCounterSliceReducers>();
        
        services.AddDuckyStore(builder =>
        {
            builder
                .UseDefaultMiddlewares()
                .AddReactiveEffects(effects =>
                {
                    effects
                        .Add<SimpleCounterEffect>()
                        .Add<AdvancedCounterEffect>(effect => 
                        {
                            // Configure the effect
                            effect.Threshold = 10;
                        })
                        .WithLoggingMonitor();
                });
        });

        // Assert
        ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();
        store.ShouldNotBeNull();
    }

    /// <summary>
    /// Example 2: Assembly scanning for automatic effect registration.
    /// </summary>
    [Fact]
    public void Example2_AssemblyScanning()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - Configure reactive effects with assembly scanning
        services.AddReactiveEffects(effects =>
        {
            effects
                .AddFromAssembly(typeof(ImprovedApiExampleTests).Assembly)
                .AddFromNamespace("Ducky.Reactive.Tests.ExampleEffects")
                .WithMetricsMonitor();
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
        IStore store = provider.GetRequiredService<IStore>();
        store.ShouldNotBeNull();
    }

    /// <summary>
    /// Example 3: Using enhanced base classes for effects.
    /// </summary>
    [Fact]
    public void Example3_EnhancedBaseClasses()
    {
        // Arrange & Act
        var scheduler = new TestScheduler();
        var effect = new TypedStateEffect();

        TestScenarioResult result = ReactiveEffectTestHelper.CreateScenario(scheduler)
            .WithActions(
                (100, new IncrementCounter()),
                (200, new IncrementCounter()),
                (300, new ResetCounter()))
            .WithStates(
                (0, CreateMockStateProvider(new TestCounterState { Value = 0 })),
                (150, CreateMockStateProvider(new TestCounterState { Value = 1 })),
                (250, CreateMockStateProvider(new TestCounterState { Value = 2 })),
                (350, CreateMockStateProvider(new TestCounterState { Value = 0 })))
            .Run(effect);

        // Assert
        result.Actions.Count().ShouldBeGreaterThan(0); // At least one event
    }

    private static IStateProvider CreateMockStateProvider(TestCounterState counterState)
    {
        IStateProvider mock = A.Fake<IStateProvider>();
        A.CallTo(() => mock.GetSlice<TestCounterState>()).Returns(counterState);
        return mock;
    }

    /// <summary>
    /// Example 4: Using pattern-based effects.
    /// </summary>
    [Fact]
    public void Example4_PatternBasedEffects()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - Configure pattern-based effects
        services.AddReactiveEffects(effects =>
        {
            effects
                .Add<SearchDebouncedEffect>()
                .Add<DataPollingEffect>()
                .Add<UserWorkflowEffect>()
                .WithOptions(options =>
                {
                    options.EnableDiagnostics = true;
                    options.LogLifecycleEvents = true;
                    options.DefaultErrorBehavior = ErrorHandlingBehavior.Retry;
                })
                .WithErrorHandler<CustomErrorHandler>();
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
        IStore store = provider.GetRequiredService<IStore>();
        store.ShouldNotBeNull();
    }

    /// <summary>
    /// Example 5: Enhanced observable extensions.
    /// </summary>
    [Fact]
    public void Example5_EnhancedObservableExtensions()
    {
        // This test demonstrates that the enhanced observable extensions exist and can be used.
        // The actual behavior is tested in dedicated unit tests for each extension.
        
        // Arrange
        IObservable<int> source = Observable.Range(1, 5);

        // Act - Verify extensions can be called
        IObservable<int> rateLimited = source.RateLimit(TimeSpan.FromMilliseconds(100));
        IObservable<IList<int>> batched = source.BatchWithTimeOrCount(3, TimeSpan.FromSeconds(1));
        IObservable<IReadOnlyList<int>> accumulated = source.Accumulate(3);
        IObservable<int> withBackoff = source.RetryWithBackoff(3, TimeSpan.FromMilliseconds(100));
        IObservable<int> fallback = source.FallbackTo(_ => Observable.Return(0));

        // Assert - Just verify the extensions exist and return observables
        rateLimited.ShouldNotBeNull();
        batched.ShouldNotBeNull();
        accumulated.ShouldNotBeNull();
        withBackoff.ShouldNotBeNull();
        fallback.ShouldNotBeNull();
    }

    // Helper method for creating notifications
    private static Recorded<System.Reactive.Notification<T>> OnNext<T>(long time, T value)
    {
        return new Recorded<System.Reactive.Notification<T>>(time, System.Reactive.Notification.CreateOnNext(value));
    }
}

// Example effect implementations
public class SimpleCounterEffect : ReactiveEffectBase
{
    protected override IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IStateProvider> stateProvider)
    {
        return actions
            .OfActionType<IncrementCounter>()
            .Select(_ => new CounterIncremented());
    }
}

public class AdvancedCounterEffect : ReactiveEffectBase
{
    public int Threshold { get; set; } = 5;

    protected override IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IStateProvider> stateProvider)
    {
        return stateProvider
            .Select(state => state.GetSlice<TestCounterState>())
            .Where(state => state.Value >= Threshold)
            .Take(1)
            .Select(_ => new CounterThresholdReached());
    }
}

public class TypedStateEffect : ReactiveEffect<TestCounterState>
{
    protected override IObservable<object> HandleTyped(
        IObservable<object> actions,
        IObservable<TestCounterState> state)
    {
        return WhenPropertyEquals(
            state,
            s => s.Value % 2,
            0,
            () => Observable.Return(new EvenValueReached()));
    }
}

public class SearchDebouncedEffect()
    : DebouncedEffect<SearchQuery>(TimeSpan.FromMilliseconds(300))
{
    protected override IObservable<object> ProcessDebouncedAction(
        SearchQuery action,
        IObservable<IStateProvider> stateProvider)
    {
        return Observable.Return(new SearchExecuted(action.Query));
    }
}

public class DataPollingEffect()
    : PollingEffect<AppState>(TimeSpan.FromSeconds(30))
{
    protected override IObservable<object> GetStopSignal(IObservable<object> actions)
    {
        return actions.OfActionType<StopPolling>();
    }

    protected override IObservable<object> Poll(AppState state)
    {
        return Observable.Return(new DataFetched(DateTime.UtcNow));
    }
}

public class UserWorkflowEffect : WorkflowEffect<StartUserRegistration>
{
    protected override IObservable<object> ExecuteWorkflow(
        StartUserRegistration startAction,
        IObservable<object> actions,
        IObservable<IStateProvider> stateProvider)
    {
        return Observable.Concat(
            Step("Validate", () => Observable.Return(new ValidationCompleted())),
            Step("CreateAccount", () => Observable.Return(new AccountCreated())),
            Step("SendEmail", () => Observable.Return(new EmailSent())),
            Observable.Return(new RegistrationCompleted())
        );
    }
}

public class CustomErrorHandler : IReactiveEffectErrorHandler
{
    public Task<ErrorHandlingResult> HandleErrorAsync(
        Exception error,
        Type effectType,
        object? action)
    {
        // Custom error handling logic
        if (error is TimeoutException)
        {
            return Task.FromResult(ErrorHandlingResult.Retry(TimeSpan.FromSeconds(1)));
        }

        return Task.FromResult(ErrorHandlingResult.Continue());
    }

    public void OnRetry(Type effectType, int retryCount, Exception lastError)
    {
        // Log retry attempts
    }
}

// Test actions and state
public record IncrementCounter;
public record ResetCounter;
public record CounterIncremented;
public record CounterThresholdReached;
public record EvenValueReached;
public record SearchQuery(string Query);
public record SearchExecuted(string Query);
public record StopPolling;
public record DataFetched(DateTime Timestamp);
public record StartUserRegistration;
public record ValidationCompleted;
public record AccountCreated;
public record EmailSent;
public record RegistrationCompleted;

public record TestCounterState : IState
{
    public int Value { get; init; }
}

public record TestCounterSliceReducers : SliceReducers<TestCounterState>
{
    public override TestCounterState GetInitialState() => new() { Value = 0 };
    
    public TestCounterSliceReducers()
    {
        Reducers[typeof(IncrementCounter)] = (state, action) => state with { Value = state.Value + 1 };
        Reducers[typeof(ResetCounter)] = (state, _) => new TestCounterState { Value = 0 };
    }
}

public record AppState;

public record TestRootState(object State) : IRootState
{
    public TState GetSlice<TState>() where TState : notnull => (TState)State;
    public TState GetSlice<TState>(string key) where TState : notnull => (TState)State;
    public bool ContainsKey(string key) => true;
    public ImmutableSortedSet<string> GetKeys() => ImmutableSortedSet<string>.Empty;
    public ImmutableSortedDictionary<string, object> GetStateDictionary() => ImmutableSortedDictionary<string, object>.Empty;
}