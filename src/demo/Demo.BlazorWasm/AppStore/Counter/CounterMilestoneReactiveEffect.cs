// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using Ducky.Reactive;
using Ducky.Reactive.Middlewares.ReactiveEffects;

namespace Demo.BlazorWasm.AppStore;

/// <summary>
/// A reactive effect that monitors counter milestones using observable streams.
/// Demonstrates the recommended reactive effect pattern for stream-based scenarios
/// like debouncing, throttling, and state monitoring.
/// </summary>
/// <remarks>
/// Use reactive effects when you need:
/// - Stream composition (debounce, throttle, merge, combine)
/// - State monitoring with deduplication
/// - Complex event coordination across multiple action types
///
/// Use async effects for simpler patterns like API calls or one-shot side effects.
/// </remarks>
public class CounterMilestoneReactiveEffect : ReactiveEffect
{
    public override IObservable<object> Handle(
        IObservable<object> actions,
        IObservable<IStateProvider> stateProvider)
    {
        // Monitor counter state and emit a SetValue action when a milestone is reached.
        // This demonstrates state monitoring with DistinctUntilChanged to avoid duplicate emissions.
        return stateProvider
            .Select(sp => sp.GetSlice<CounterState>())
            .DistinctUntilChanged(state => state.Value)
            .Where(state => state.Value > 0 && state.Value % 100 == 0)
            .Select(state => (object)new CounterMilestoneReached(state.Value));
    }
}

/// <summary>
/// Action dispatched when the counter reaches a milestone (multiple of 100).
/// </summary>
[DuckyAction]
public partial record CounterMilestoneReached(int MilestoneValue);
