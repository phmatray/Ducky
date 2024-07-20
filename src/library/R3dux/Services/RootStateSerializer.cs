// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace R3dux;

/// <summary>
/// Provides methods for serializing and deserializing <see cref="RootState"/> instances.
/// </summary>
public sealed class RootStateSerializer : IRootStateSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public string Serialize(RootState rootState)
    {
        ArgumentNullException.ThrowIfNull(rootState);

        var stateDictionary = rootState.GetStateDictionary();
        var typedDictionary = stateDictionary.ToDictionary(
            kvp => kvp.Key,
            kvp => new { Type = kvp.Value.GetType().AssemblyQualifiedName, kvp.Value });

        return JsonSerializer.Serialize(typedDictionary, _options);
    }

    /// <inheritdoc />
    public string Serialize(RootState rootState, string key)
    {
        ArgumentNullException.ThrowIfNull(rootState);
        ArgumentNullException.ThrowIfNull(key);

        var stateDictionary = rootState.GetStateDictionary();
        var typedDictionary = stateDictionary.ToDictionary(
            kvp => kvp.Key,
            kvp => new { Type = kvp.Value.GetType().AssemblyQualifiedName, kvp.Value });

        return JsonSerializer.Serialize(typedDictionary[key], _options);
    }

    /// <inheritdoc />
    public RootState Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var typedDictionary =
            JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json, _options)
            ?? [];

        var state = new Dictionary<string, object>();

        foreach (var kvp in typedDictionary)
        {
            var typeName =
                kvp.Value["type"].ToString()
                ?? throw new InvalidOperationException("Type not found.");

            var type =
                Type.GetType(typeName)
                ?? throw new InvalidOperationException($"Type '{typeName}' not found.");

            var valueJson =
                kvp.Value["value"].ToString()
                ?? throw new InvalidOperationException("Value not found.");

            var value =
                JsonSerializer.Deserialize(valueJson, type, _options)
                ?? throw new InvalidOperationException("Value not deserialized.");

            state[kvp.Key] = value;
        }

        return new RootState(state.ToImmutableSortedDictionary());
    }

    /// <inheritdoc />
    public void SaveToFile(RootState rootState, string filePath)
    {
        ArgumentNullException.ThrowIfNull(rootState);
        ArgumentNullException.ThrowIfNull(filePath);

        File.WriteAllText(filePath, Serialize(rootState));
    }

    /// <inheritdoc />
    public RootState LoadFromFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file '{filePath}' does not exist.");
        }

        var json = File.ReadAllText(filePath);
        return Deserialize(json);
    }
}
