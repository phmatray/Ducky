// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Provides read-only access to application state slices.
/// </summary>
public interface IStateProvider
{
    /// <summary>
    /// Gets the state of a specific slice by its type.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <returns>The current state of the slice.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the slice is not found.</exception>
    TState GetSlice<TState>();

    /// <summary>
    /// Gets the state of a specific slice by its key.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <param name="key">The slice key.</param>
    /// <returns>The current state of the slice.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the slice key is not found.</exception>
    TState GetSliceByKey<TState>(string key);

    /// <summary>
    /// Attempts to get the state of a specific slice by its type.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <param name="state">When this method returns, contains the state of the slice if found; otherwise, the default value.</param>
    /// <returns><c>true</c> if the slice was found; otherwise, <c>false</c>.</returns>
    bool TryGetSlice<TState>(out TState? state);

    /// <summary>
    /// Checks whether a slice of the specified type exists.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <returns><c>true</c> if the slice exists; otherwise, <c>false</c>.</returns>
    bool HasSlice<TState>();

    /// <summary>
    /// Checks whether a slice with the specified key exists.
    /// </summary>
    /// <param name="key">The slice key.</param>
    /// <returns><c>true</c> if the slice exists; otherwise, <c>false</c>.</returns>
    bool HasSliceByKey(string key);

    /// <summary>
    /// Gets all slice keys.
    /// </summary>
    /// <returns>A read-only collection of slice keys.</returns>
    IReadOnlyCollection<string> GetSliceKeys();

    /// <summary>
    /// Gets all slices as key-value pairs.
    /// </summary>
    /// <returns>A read-only dictionary of slice keys and their states.</returns>
    IReadOnlyDictionary<string, object> GetAllSlices();
}
