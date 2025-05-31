// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Provides methods for serializing and deserializing <see cref="IRootState"/> instances.
/// </summary>
public interface IRootStateSerializer
{
    /// <summary>
    /// Serializes the specified <see cref="IRootState"/> to a JSON string.
    /// </summary>
    /// <param name="rootState">The <see cref="IRootState"/> to serialize.</param>
    /// <returns>The JSON string representation of the state.</returns>
    string Serialize(IRootState rootState);

    /// <summary>
    /// Serializes the slice state associated with the specified key to a JSON string.
    /// </summary>
    /// <param name="rootState">The <see cref="IRootState"/> to serialize.</param>
    /// <param name="key">The key of the slice state to serialize.</param>
    /// <returns>The JSON string representation of the slice state.</returns>
    string Serialize(IRootState rootState, string key);

    /// <summary>
    /// Deserializes a <see cref="IRootState"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string representation of the state.</param>
    /// <returns>A new instance of <see cref="IRootState"/> with the deserialized state.</returns>
    IRootState Deserialize(string json);
}
