// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;

namespace Ducky;

/// <summary>
/// Defines the contract for a dispatcher that can dispatch actions and provide an observable stream of dispatched actions.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Gets an observable stream of dispatched actions.
    /// </summary>
    Observable<IAction> ActionStream { get; }

    /// <summary>
    /// Dispatches the specified action.
    /// </summary>
    /// <param name="action">The action to dispatch.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the dispatcher has been disposed.</exception>
    void Dispatch(IAction action);
}
