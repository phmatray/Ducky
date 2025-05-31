// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.ReactiveEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that simulates errors for testing the error handling system.
/// </summary>
public class TestErrorEffect : ReactiveEffect
{
    public override Observable<object> Handle(
        Observable<object> actions,
        Observable<IRootState> rootState)
    {
        return actions.OfType<object, TestErrorAction>()
            .Do(action => throw new ApplicationException(action.ErrorMessage))
            .Cast<TestErrorAction, object>()
            .IgnoreElements();
    }
}
