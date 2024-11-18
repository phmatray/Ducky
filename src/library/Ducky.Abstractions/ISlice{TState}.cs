// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;

namespace Ducky;

/// <summary>
/// Represents a strongly-typed state slice with state management and reducers.
/// </summary>
/// <typeparam name="TState">The type of the state managed by this slice.</typeparam>
public interface ISlice<TState> : ISlice
{
    /// <summary>
    /// Gets an observable sequence that produces the state of this slice.
    /// </summary>
    /// <value>The observable sequence of the state.</value>
    Observable<TState> State { get; }

    /// <summary>
    /// Gets the collection of reducers for this state slice.
    /// </summary>
    /// <value>The collection of reducers.</value>
    Dictionary<Type, Func<TState, IAction, TState>> Reducers { get; }
}
