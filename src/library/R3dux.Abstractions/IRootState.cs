// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace R3dux;

/// <summary>
/// Represents the root state of the application, managing slice states.
/// </summary>
public interface IRootState
{
    /// <summary>
    /// Gets the underlying state dictionary for serialization purposes.
    /// </summary>
    /// <returns>The state dictionary.</returns>
    ImmutableSortedDictionary<string, object> GetStateDictionary();

    /// <summary>
    /// Gets the keys of the state.
    /// </summary>
    /// <returns>The keys of the state.</returns>
    ImmutableSortedSet<string> GetKeys();

    /// <summary>
    /// Gets the slice state associated with the specified key.
    /// </summary>
    /// <typeparam name="TState">The type of the state to select.</typeparam>
    /// <param name="key">The key of the state to select.</param>
    /// <returns>The state associated with the specified key.</returns>
    TState GetSliceState<TState>(string key)
        where TState : notnull;

    /// <summary>
    /// Gets the slice state of the specified type.
    /// </summary>
    /// <typeparam name="TState">The type of the state to select.</typeparam>
    /// <returns>The state of the specified type.</returns>
    TState GetSliceState<TState>()
        where TState : notnull;

    /// <summary>
    /// Determines whether the state contains an element with the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the state.</param>
    /// <returns><c>true</c> if the state contains an element with the key; otherwise, <c>false</c>.</returns>
    bool ContainsKey(string key);
}
