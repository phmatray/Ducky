// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Blazor.Router;

/// <summary>
/// An action dispatched when the router errors.
/// </summary>
public sealed record RouterErrorAction : Fsa
{
    /// <inheritdoc />
    public override string TypeKey => "router-store/error";
}
