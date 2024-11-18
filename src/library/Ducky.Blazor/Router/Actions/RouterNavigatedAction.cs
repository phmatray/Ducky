// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Extensions.FluxStandardActions;

namespace Ducky.Blazor.Router.Actions;

/// <summary>
/// An action dispatched after navigation has ended and new route is active.
/// </summary>
public sealed record RouterNavigatedAction : Fsa
{
    /// <inheritdoc />
    public override string TypeKey => "router-store/navigated";
}
