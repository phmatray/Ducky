// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that handles the LoadMoviesFailure action.
/// </summary>
/// <param name="snackbar">The snackbar service.</param>
// ReSharper disable once UnusedType.Global
public class LoadMoviesFailureEffect(ISnackbar snackbar) : AsyncEffect<LoadMoviesFailure>
{
    /// <inheritdoc />
    public override Task HandleAsync(LoadMoviesFailure action, IRootState rootState)
    {
        // Show error in snackbar
        snackbar.Add(action.ErrorMessage, Severity.Error);

        // Create and dispatch error notification
        ErrorNotification notification = new(action.ErrorMessage);
        Dispatcher.AddNotification(notification);

        return Task.CompletedTask;
    }
}
