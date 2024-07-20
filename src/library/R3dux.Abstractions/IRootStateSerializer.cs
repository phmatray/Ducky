// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Abstractions;

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

    /// <summary>
    /// Saves the specified <see cref="IRootState"/> to a file.
    /// </summary>
    /// <param name="rootState">The <see cref="IRootState"/> to save.</param>
    /// <param name="filePath">The path of the file to save the state to.</param>
    void SaveToFile(IRootState rootState, string filePath);

    /// <summary>
    /// Loads a <see cref="IRootState"/> from a file.
    /// </summary>
    /// <param name="filePath">The path of the file to load the state from.</param>
    /// <returns>A new instance of <see cref="IRootState"/> with the loaded state.</returns>
    IRootState LoadFromFile(string filePath);
}
