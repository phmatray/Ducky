// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace R3dux;

/// <summary>
/// Factory for creating instances of <see cref="R3duxStore"/>.
/// </summary>
public static class StoreFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="R3duxStore"/>.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to be used by the store.</param>
    /// <param name="logger">The logger to be used by the store.</param>
    /// <param name="slices">The collection of slices to be added to the store.</param>
    /// <param name="effects">The collection of effects to be added to the store.</param>
    /// <returns>A new instance of <see cref="R3duxStore"/>.</returns>
    public static R3duxStore CreateStore(
        IDispatcher dispatcher,
        ILogger<R3duxStore> logger,
        ISlice[] slices,
        IEffect[] effects)
    {
        var store = new R3duxStore(dispatcher, logger);

        store.AddSlices(slices);
        store.AddEffects(effects);

        return store;
    }
}
