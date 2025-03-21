// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that handles the LoadMoviesSuccess action.
/// </summary>
/// <param name="snackbar">The snackbar service.</param>
// ReSharper disable once UnusedType.Global
public class LoadMoviesSuccessEffect(ISnackbar snackbar) : ReactiveEffect
{
    /// <inheritdoc />
    public override Observable<object> Handle(
        Observable<object> actions, Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<LoadMoviesSuccess>()
            .Select(GetSnackBarMessage)
            .Do(message => snackbar.Add(message, Severity.Success))
            .Select(object (message) =>
            {
                SuccessNotification notification = new(message);
                return new AddNotification(notification);
            });
    }

    private static string GetSnackBarMessage(LoadMoviesSuccess action)
    {
        return $"Loaded {action.Movies.Count} movies from the server.";
    }
}
