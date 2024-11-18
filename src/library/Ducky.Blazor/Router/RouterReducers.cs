// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Blazor.Router;

#region State

/// <summary>
/// The state of the router store.
/// </summary>
public record RouterStoreState
{
    /// <summary>
    /// Gets or init the current route.
    /// </summary>
    public required Uri Url { get; init; }
}

#endregion

#region Reducers

/// <summary>
/// The router store reducers.
/// </summary>
public class RouterReducers;

#endregion
