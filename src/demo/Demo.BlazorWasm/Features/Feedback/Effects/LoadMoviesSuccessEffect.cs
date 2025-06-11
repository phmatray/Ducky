// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that handles the LoadMoviesSuccess action.
/// </summary>
/// <param name="snackbar">The snackbar service.</param>
// ReSharper disable once UnusedType.Global
public class LoadMoviesSuccessEffect(ISnackbar snackbar) : AsyncEffect<LoadMoviesSuccess>
{
    public override Task HandleAsync(LoadMoviesSuccess action, IRootState rootState)
    {
        string snackBarMessage = GetSnackBarMessage(action);
        snackbar.Add(snackBarMessage, Severity.Success);

        SuccessNotification notification = new(snackBarMessage);
        Dispatcher?.Dispatch(new AddNotification(notification));

        return Task.CompletedTask;
    }

    private static string GetSnackBarMessage(LoadMoviesSuccess action)
    {
        return $"Loaded {action.Movies.Count} movies from the server.";
    }
}
