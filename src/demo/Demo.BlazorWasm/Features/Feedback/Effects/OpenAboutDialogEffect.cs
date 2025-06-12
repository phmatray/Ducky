// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that handles the OpenAboutDialog action.
/// </summary>
/// <param name="dialog">The dialog service.</param>
// ReSharper disable once UnusedType.Global
public class OpenAboutDialogEffect(IDialogService dialog) : AsyncEffect<OpenAboutDialog>
{
    /// <inheritdoc />
    public override async Task HandleAsync(OpenAboutDialog action, IRootState rootState)
    {
        DialogOptions options = new() { CloseOnEscapeKey = true };

        // Show the about dialog
        await dialog.ShowAsync<AboutDialog>(null, options);

        // Optionally dispatch a NoOp action to indicate completion
        Dispatcher.NoOp();
    }
}
