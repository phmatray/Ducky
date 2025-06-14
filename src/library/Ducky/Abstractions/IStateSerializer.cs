// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Provides methods for serializing and deserializing state instances.
/// </summary>
public interface IStateSerializer
{
    /// <summary>
    /// Serializes the specified state provider to a JSON string.
    /// </summary>
    /// <param name="stateProvider">The state provider to serialize.</param>
    /// <returns>The JSON string representation of the state.</returns>
    string Serialize(IStateProvider stateProvider);

    /// <summary>
    /// Serializes the slice state associated with the specified key to a JSON string.
    /// </summary>
    /// <param name="stateProvider">The state provider to serialize.</param>
    /// <param name="key">The key of the slice state to serialize.</param>
    /// <returns>The JSON string representation of the slice state.</returns>
    string Serialize(IStateProvider stateProvider, string key);

    /// <summary>
    /// Deserializes state from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string representation of the state.</param>
    /// <returns>A new instance of <see cref="RootState"/> with the deserialized state.</returns>
    RootState Deserialize(string json);
}
