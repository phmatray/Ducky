// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
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
    public override async Task HandleAsync(
        OpenAboutDialog action,
        IStateProvider stateProvider,
        CancellationToken cancellationToken = default)
    {
        DialogOptions options = new() { CloseOnEscapeKey = true };

        // Show the about dialog
        await dialog.ShowAsync<AboutDialog>(null, options);

        // Optionally dispatch a NoOp action to indicate completion
        Dispatcher.NoOp();
    }
}
