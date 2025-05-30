// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.ReactiveEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that handles all actions.
/// </summary>
public class AllActionsEffect : ReactiveEffect
{
    /// <inheritdoc />
    public override Observable<object> Handle(
        Observable<object> actions, Observable<IRootState> rootState)
    {
        // Log all actions to console (side effect only, no new actions dispatched)
        actions
            .Subscribe(action => Console.WriteLine($"[AllActionsEffect] Action: {action.GetType().Name}"));

        // Return empty observable since this effect only logs
        return Observable.Empty<object>();
    }
}
