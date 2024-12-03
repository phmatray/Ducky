// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.Website2.Components.Shared;
using Demo.Website2.Features.Feedback.Actions;

namespace Demo.Website2.Features.Feedback.Effects;

/// <summary>
/// Effect that handles the OpenAboutDialog action.
/// </summary>
/// <param name="dialog">The dialog service.</param>
// ReSharper disable once UnusedType.Global
public class OpenAboutDialogEffect(IDialogService dialog) : ReactiveEffect
{
    /// <inheritdoc />
    public override Observable<IAction> Handle(
        Observable<IAction> actions, Observable<IRootState> rootState)
    {
        DialogOptions options = new() { CloseOnEscapeKey = true };

        return actions
            .OfActionType<OpenAboutDialog>()
            .Do(_ => dialog.ShowAsync<AboutDialog>(null, options))
            .Select(IAction (_) => new NoOp());
    }
}
