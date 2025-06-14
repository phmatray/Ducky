// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Provides persistence operations for application state.
/// </summary>
public interface IPersistenceService
{
    /// <summary>
    /// Gets a value indicating whether hydration has completed.
    /// </summary>
    bool IsHydrated { get; }

    /// <summary>
    /// Gets a value indicating whether persistence is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Hydrates the store from persisted state.
    /// </summary>
    /// <returns>A task that represents the asynchronous hydration operation.</returns>
    Task HydrateAsync();

    /// <summary>
    /// Persists the current state.
    /// </summary>
    /// <returns>A task that represents the asynchronous persistence operation.</returns>
    Task PersistAsync();

    /// <summary>
    /// Clears the persisted state.
    /// </summary>
    /// <returns>A task that represents the asynchronous clear operation.</returns>
    Task ClearAsync();
}
