// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Represents a persisted state dictionary with type information for each slice.
/// </summary>
public class PersistedStateDictionary
{
    /// <summary>
    /// Gets or sets the slices with their type information.
    /// </summary>
    [JsonPropertyName("slices")]
    public Dictionary<string, PersistedSlice> Slices { get; set; } = [];
}

/// <summary>
/// Represents a persisted slice with type information.
/// </summary>
public class PersistedSlice
{
    /// <summary>
    /// Gets or sets the type name of the slice.
    /// </summary>
    [JsonPropertyName("typeName")]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON representation of the slice state.
    /// </summary>
    [JsonPropertyName("stateJson")]
    public JsonElement StateJson { get; set; }
}
