// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;

namespace R3dux.Abstractions;

/// <summary>
/// Represents an effect that handles a stream of actions and interacts with the store's state.
/// </summary>
public interface IEffect
{
    /// <summary>
    /// Gets the key that identifies the effect.
    /// </summary>
    /// <returns>The key that identifies the effect.</returns>
    string GetKey();

    /// <summary>
    /// Gets the assembly-qualified name of the effect.
    /// </summary>
    /// <returns>The assembly-qualified name of the effect.</returns>
    string GetAssemblyName();

    /// <summary>
    /// Handles a stream of actions and produces a stream of resulting actions.
    /// </summary>
    /// <param name="actions">The source observable sequence of actions.</param>
    /// <param name="rootState">The source observable sequence of the root state.</param>
    /// <returns>An observable sequence of resulting actions.</returns>
    Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<IRootState> rootState);
}
