// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Reactive.Testing;

namespace Ducky.Reactive.Testing;

/// <summary>
/// Helper class for testing reactive effects with TestScheduler.
/// </summary>
public class ReactiveEffectTestHelper
{
    private readonly TestScheduler _scheduler;
    private readonly List<Recorded<Notification<object>>> _actions;
    private readonly List<Recorded<Notification<IRootState>>> _states;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveEffectTestHelper"/> class.
    /// </summary>
    /// <param name="scheduler">The test scheduler to use.</param>
    public ReactiveEffectTestHelper(TestScheduler scheduler)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _actions = new List<Recorded<Notification<object>>>();
        _states = new List<Recorded<Notification<IRootState>>>();
    }

    /// <summary>
    /// Adds an action to be emitted at a specific time.
    /// </summary>
    /// <param name="time">The virtual time to emit the action.</param>
    /// <param name="action">The action to emit.</param>
    /// <returns>The helper for chaining.</returns>
    public ReactiveEffectTestHelper EmitAction(long time, object action)
    {
        _actions.Add(new Recorded<Notification<object>>(time, Notification.CreateOnNext(action)));
        return this;
    }

    /// <summary>
    /// Adds a state update at a specific time.
    /// </summary>
    /// <param name="time">The virtual time to emit the state.</param>
    /// <param name="state">The state to emit.</param>
    /// <returns>The helper for chaining.</returns>
    public ReactiveEffectTestHelper EmitState(long time, IRootState state)
    {
        _states.Add(new Recorded<Notification<IRootState>>(time, Notification.CreateOnNext(state)));
        return this;
    }

    /// <summary>
    /// Completes the action stream at a specific time.
    /// </summary>
    /// <param name="time">The virtual time to complete.</param>
    /// <returns>The helper for chaining.</returns>
    public ReactiveEffectTestHelper CompleteActions(long time)
    {
        _actions.Add(new Recorded<Notification<object>>(time, Notification.CreateOnCompleted<object>()));
        return this;
    }

    /// <summary>
    /// Completes the state stream at a specific time.
    /// </summary>
    /// <param name="time">The virtual time to complete.</param>
    /// <returns>The helper for chaining.</returns>
    public ReactiveEffectTestHelper CompleteStates(long time)
    {
        _states.Add(new Recorded<Notification<IRootState>>(time, Notification.CreateOnCompleted<IRootState>()));
        return this;
    }

    /// <summary>
    /// Tests a reactive effect and returns the recorded output actions.
    /// </summary>
    /// <param name="effect">The effect to test.</param>
    /// <param name="subscribeAt">The time to subscribe to the effect.</param>
    /// <param name="disposeAt">The time to dispose the subscription.</param>
    /// <returns>The observer containing recorded actions.</returns>
    public ITestableObserver<object> TestEffect(
        ReactiveEffect effect,
        long subscribeAt = 0,
        long disposeAt = 1000)
    {
        ITestableObservable<object> actionsObservable = _scheduler.CreateHotObservable(_actions.ToArray());
        ITestableObservable<IRootState> statesObservable = _scheduler.CreateHotObservable(_states.ToArray());

        IObservable<object> output = effect.Handle(actionsObservable, statesObservable);
        ITestableObserver<object> observer = _scheduler.CreateObserver<object>();

        _scheduler.ScheduleAbsolute(
            subscribeAt,
            () =>
            {
                IDisposable subscription = output.Subscribe(observer);
                _scheduler.ScheduleAbsolute(disposeAt, () => subscription.Dispose());
            });

        _scheduler.Start();

        return observer;
    }

    /// <summary>
    /// Creates a test scenario builder for more complex test setups.
    /// </summary>
    /// <returns>A new test scenario builder.</returns>
    public static TestScenarioBuilder CreateScenario(TestScheduler scheduler)
    {
        return new(scheduler);
    }
}

/// <summary>
/// Builder for creating complex test scenarios.
/// </summary>
public class TestScenarioBuilder
{
    private readonly TestScheduler _scheduler;
    private readonly ReactiveEffectTestHelper _helper;

    internal TestScenarioBuilder(TestScheduler scheduler)
    {
        _scheduler = scheduler;
        _helper = new ReactiveEffectTestHelper(scheduler);
    }

    /// <summary>
    /// Adds an action sequence to the scenario.
    /// </summary>
    /// <param name="actions">The actions with their emission times.</param>
    /// <returns>The builder for chaining.</returns>
    public TestScenarioBuilder WithActions(params (long time, object action)[] actions)
    {
        foreach ((long time, object action) in actions)
        {
            _helper.EmitAction(time, action);
        }

        return this;
    }

    /// <summary>
    /// Adds a state sequence to the scenario.
    /// </summary>
    /// <param name="states">The states with their emission times.</param>
    /// <returns>The builder for chaining.</returns>
    public TestScenarioBuilder WithStates(params (long time, IRootState state)[] states)
    {
        foreach ((long time, IRootState state) in states)
        {
            _helper.EmitState(time, state);
        }

        return this;
    }

    /// <summary>
    /// Runs the scenario and returns the results.
    /// </summary>
    /// <param name="effect">The effect to test.</param>
    /// <returns>The test results.</returns>
    public TestScenarioResult Run(ReactiveEffect effect)
    {
        ITestableObserver<object> observer = _helper.TestEffect(effect);
        return new TestScenarioResult(_scheduler, observer);
    }
}

/// <summary>
/// Results from running a test scenario.
/// </summary>
public class TestScenarioResult
{
    private readonly TestScheduler _scheduler;
    private readonly ITestableObserver<object> _observer;

    internal TestScenarioResult(TestScheduler scheduler, ITestableObserver<object> observer)
    {
        _scheduler = scheduler;
        _observer = observer;
    }

    /// <summary>
    /// Gets the recorded messages.
    /// </summary>
    public IList<Recorded<Notification<object>>> Messages => _observer.Messages;

    /// <summary>
    /// Gets the emitted actions.
    /// </summary>
    public IEnumerable<object> Actions => Messages
        .Where(m => m.Value.HasValue)
        .Select(m => m.Value.Value);

    /// <summary>
    /// Gets the virtual time of the scheduler.
    /// </summary>
    public long VirtualTime => _scheduler.Clock;

    /// <summary>
    /// Asserts that specific actions were emitted at specific times.
    /// </summary>
    /// <param name="expected">The expected actions with their times.</param>
    public void AssertActions(params (long time, object action)[] expected)
    {
        (long Time, object Value)[] actual = Messages
            .Where(m => m.Value.HasValue)
            .Select(m => (m.Time, m.Value.Value))
            .ToArray();

        if (actual.Length != expected.Length)
        {
            throw new InvalidOperationException(
                $"Expected {expected.Length} actions but got {actual.Length}");
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[i].Time != expected[i].time || !actual[i].Value.Equals(expected[i].action))
            {
                throw new InvalidOperationException(
                    $"Expected {expected[i].action} at {expected[i].time} but got {actual[i].Value} at {actual[i].Time}");
            }
        }
    }
}
