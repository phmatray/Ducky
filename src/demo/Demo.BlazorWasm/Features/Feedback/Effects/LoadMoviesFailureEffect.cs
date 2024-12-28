// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that handles the LoadMoviesFailure action.
/// </summary>
/// <param name="snackbar">The snackbar service.</param>
// ReSharper disable once UnusedType.Global
public class LoadMoviesFailureEffect(ISnackbar snackbar) : ReactiveEffect
{
    /// <inheritdoc />
    public override Observable<object> Handle(
        Observable<object> actions, Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<LoadMoviesFailure>()
            .Do(action => snackbar.Add(action.Error.Message, Severity.Error))
            .Select(object (action) =>
            {
                ExceptionNotification notification = new(action.Error);
                return new AddNotification(notification);
            });
    }
}
