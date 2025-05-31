// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text.Json;

namespace Ducky;

/// <summary>
/// Provides methods for serializing and deserializing <see cref="RootState"/> instances.
/// </summary>
public sealed class RootStateSerializer : IRootStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public string Serialize(IRootState rootState)
    {
        ArgumentNullException.ThrowIfNull(rootState);

        ImmutableSortedDictionary<string, object> stateDictionary = rootState.GetStateDictionary();
        var typedDictionary = stateDictionary.ToDictionary(
            kvp => kvp.Key,
            kvp => new { Type = kvp.Value.GetType().AssemblyQualifiedName, kvp.Value });

        return JsonSerializer.Serialize(typedDictionary, Options);
    }

    /// <inheritdoc />
    public string Serialize(IRootState rootState, string key)
    {
        ArgumentNullException.ThrowIfNull(rootState);
        ArgumentNullException.ThrowIfNull(key);

        ImmutableSortedDictionary<string, object> stateDictionary = rootState.GetStateDictionary();
        var typedDictionary = stateDictionary.ToDictionary(
            kvp => kvp.Key,
            kvp => new { Type = kvp.Value.GetType().AssemblyQualifiedName, kvp.Value });

        return JsonSerializer.Serialize(typedDictionary[key], Options);
    }

    /// <inheritdoc />
    public IRootState Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        Dictionary<string, Dictionary<string, object>> typedDictionary =
            JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json, Options) ?? [];

        Dictionary<string, object> state = [];

        foreach (KeyValuePair<string, Dictionary<string, object>> kvp in typedDictionary)
        {
            string typeName = kvp.Value["type"].ToString()
                ?? throw new InvalidOperationException("Type not found.");

            Type type = Type.GetType(typeName)
                ?? throw new InvalidOperationException($"Type '{typeName}' not found.");

            string valueJson = kvp.Value["value"].ToString()
                ?? throw new InvalidOperationException("Value not found.");

            object value = JsonSerializer.Deserialize(valueJson, type, Options)
                ?? throw new InvalidOperationException("Value not deserialized.");

            state[kvp.Key] = value;
        }

        return new RootState(state.ToImmutableSortedDictionary());
    }
}
