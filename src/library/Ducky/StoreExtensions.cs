// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Extension methods for IStore to ease the transition from IRootState to IStateProvider.
/// </summary>
public static class StoreExtensions
{
    /// <summary>
    /// Gets the current state as IRootState for backward compatibility.
    /// This is a temporary bridge method during the transition to IStateProvider.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <returns>An IRootState adapter wrapping the store's state provider.</returns>
    public static IRootState CurrentState(this IStore store)
    {
        return new StateProviderAdapter(store);
    }
}
