// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace R3dux;

/// <summary>
/// Factory for creating instances of <see cref="Store"/>.
/// </summary>
public sealed class StoreFactory : IStoreFactory
{
    /// <inheritdoc />
    public Store CreateStore(
        IDispatcher dispatcher,
        ILogger<Store> logger,
        ISlice[] slices,
        IEffect[] effects)
    {
        var store = new Store(dispatcher, logger);

        store.AddSlices(slices);
        store.AddEffects(effects);

        return store;
    }
}
