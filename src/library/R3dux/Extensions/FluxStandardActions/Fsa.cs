// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux;

/// <summary>
/// A Flux Standard action without payload or metadata properties.
/// </summary>
public abstract record Fsa
    : IAction
{
    /// <summary>
    /// Gets the `type` of an action identifies to the consumer the nature of the action that has occurred.
    /// </summary>
    public abstract string TypeKey { get; }
}
