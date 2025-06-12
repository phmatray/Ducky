// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;

namespace Ducky.Reactive.Tests;

/// <summary>
/// Example reactive effects demonstrating various patterns and use cases.
/// </summary>
public static class ExampleReactiveEffects
{
    /// <summary>
    /// A timer effect that demonstrates reactive timer management using observables.
    /// </summary>
    public class TimerEffect : ReactiveEffect
    {
        private readonly IScheduler _scheduler;

        public TimerEffect(IScheduler? scheduler = null)
        {
            _scheduler = scheduler ?? new EventLoopScheduler();
        }

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            return actions
                .OfActionType<StartTimer>()
                .SelectMany(_ => Observable.Interval(TimeSpan.FromSeconds(1), _scheduler)
                    .Select(_ => new Tick())
                    .Cast<object>()
                    .TakeUntil(actions.OfActionType<StopTimer>()));
        }
    }

    /// <summary>
    /// A debounced search effect that waits for typing to stop before performing search.
    /// </summary>
    public class DebouncedSearchEffect : ReactiveEffect
    {
        private readonly IScheduler _scheduler;

        public DebouncedSearchEffect(IScheduler? scheduler = null)
        {
            _scheduler = scheduler ?? Scheduler.Default;
        }

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            return actions
                .OfActionType<SearchTextChanged>()
                .Where(action => !string.IsNullOrWhiteSpace(action.SearchText))
                .Throttle(TimeSpan.FromMilliseconds(300), _scheduler)
                .SelectMany(action => PerformSearch(action.SearchText)
                    .Select(results => (object)new SearchCompleted(results))
                    .StartWith(new SearchStarted())
                    .Catch<object, Exception>(ex => Observable.Return((object)new SearchFailed(ex.Message))));
        }

        private IObservable<string[]> PerformSearch(string searchText)
        {
            // Simulate async search operation
            return Observable.Return(new[] { $"Result for '{searchText}' #1", $"Result for '{searchText}' #2" })
                .Delay(TimeSpan.FromMilliseconds(100), _scheduler);
        }
    }

    /// <summary>
    /// An API effect that handles HTTP requests with automatic retry logic.
    /// </summary>
    public class RetryableApiEffect : ReactiveEffect
    {
        private const int MaxRetries = 3;

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            return actions
                .OfActionType<ApiRequest>()
                .SelectMany(request =>
                    PerformApiCall(request)
                        .Select(response => (object)new ApiSuccess(response))
                        .Retry(MaxRetries)
                        .Catch<object, Exception>(ex => Observable.Return((object)new ApiFailed(ex.Message)))
                        .StartWith(new ApiStarted()));
        }

        private IObservable<string> PerformApiCall(ApiRequest request)
        {
            // Simulate API call that might fail
            return Observable
                .Create<string>(observer =>
                {
                    Random random = new();
                    if (random.NextDouble() < 0.7) // 70% success rate
                    {
                        observer.OnNext($"API response for {request.Endpoint}");
                        observer.OnCompleted();
                    }
                    else
                    {
                        observer.OnError(new HttpRequestException("Simulated API failure"));
                    }

                    return () => { }; // Cleanup action
                })
                .Delay(TimeSpan.FromMilliseconds(200));
        }
    }

    /// <summary>
    /// A state-dependent effect that reacts to specific state changes.
    /// </summary>
    public class StateMonitoringEffect : ReactiveEffect
    {
        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            return rootState
                .Select(state => state.GetSliceState<CounterState>())
                .DistinctUntilChanged(state => state.Value)
                .Where(state => state.Value % 5 == 0 && state.Value > 0)
                .Select(state => (object)new ThresholdReached(state.Value));
        }
    }

    /// <summary>
    /// A complex workflow effect that demonstrates chaining multiple operations.
    /// </summary>
    public class ComplexWorkflowEffect : ReactiveEffect
    {
        private readonly IScheduler _scheduler;

        public ComplexWorkflowEffect(IScheduler? scheduler = null)
        {
            _scheduler = scheduler ?? Scheduler.Default;
        }

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            return actions
                .OfActionType<StartWorkflow>()
                .SelectMany(_ => ExecuteWorkflow());
        }

        private IObservable<object> ExecuteWorkflow()
        {
            return Observable.Concat(
                Observable.Return((object)new WorkflowStepCompleted(1)),
                Observable.Return((object)new WorkflowStepCompleted(2))
                    .Delay(TimeSpan.FromMilliseconds(100), _scheduler),
                Observable.Return((object)new WorkflowStepCompleted(3))
                    .Delay(TimeSpan.FromMilliseconds(200), _scheduler),
                Observable.Return((object)new WorkflowCompleted()).Delay(TimeSpan.FromMilliseconds(300), _scheduler)
            );
        }
    }

    /// <summary>
    /// A cancellable effect that demonstrates proper cleanup and cancellation.
    /// </summary>
    public class CancellableEffect : ReactiveEffect
    {
        private readonly IScheduler _scheduler;

        public CancellableEffect(IScheduler? scheduler = null)
        {
            _scheduler = scheduler ?? Scheduler.Default;
        }

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            return actions
                .OfActionType<StartLongRunningTask>()
                .SelectMany(_ =>
                {
                    IObservable<CancelLongRunningTask> cancelSignal = actions.OfActionType<CancelLongRunningTask>();

                    IObservable<object> progress = Observable.Interval(TimeSpan.FromSeconds(1), _scheduler)
                        .Select(i => (object)new LongRunningTaskProgress((int)i + 1))
                        .TakeUntil(cancelSignal)
                        .Take(10);

                    IObservable<object> cancelled = cancelSignal
                        .Select(_ => (object)new LongRunningTaskCancelled())
                        .Take(1);

                    return Observable.Merge(progress, cancelled);
                });
        }
    }
}

// Test action types
public record StartTimer;
public record StopTimer;
public record Tick;

public record SearchTextChanged(string SearchText);
public record SearchStarted;
public record SearchCompleted(string[] Results);
public record SearchFailed(string Error);

public record ApiRequest(string Endpoint);
public record ApiStarted;
public record ApiSuccess(string Response);
public record ApiFailed(string Error);

public record CounterState(int Value);
public record ThresholdReached(int Value);

public record StartWorkflow;
public record WorkflowStepCompleted(int StepNumber);
public record WorkflowCompleted;

public record StartLongRunningTask;
public record CancelLongRunningTask;
public record LongRunningTaskProgress(int Progress);
public record LongRunningTaskCancelled;