// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Blazor.Router.Actions;

// original doc: https://ngrx.io/guide/router-store/actions
// original src: https://github.com/ngrx/platform/tree/main/modules/router-store/src

/// <summary>
/// An action dispatched when a router navigation request is fired.
/// </summary>
public sealed record RouterRequestAction(RouterRequestAction.ActionPayload Payload)
    : Fsa<RouterRequestAction.ActionPayload>(Payload)
{
    /// <inheritdoc />
    public override string TypeKey => "router-store/request";

    /// <summary>
    /// The payload for the RouterRequestAction.
    /// </summary>
    /// <param name="RouterState">The current router state.</param>
    /// <param name="Event">The event that triggered the navigation.</param>
    public sealed record ActionPayload(object RouterState, object Event);
}
