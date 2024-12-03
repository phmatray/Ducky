// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Factory for creating instances of <see cref="DuckyStore"/>.
/// </summary>
public static class DuckyStoreFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="DuckyStore"/>.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to be used by the store.</param>
    /// <param name="slices">The collection of slices to be added to the store.</param>
    /// <param name="effects">The collection of effects to be added to the store.</param>
    /// <param name="reactiveEffects">The collection of reactive effects to be added to the store.</param>
    /// <returns>A new instance of <see cref="DuckyStore"/>.</returns>
    public static DuckyStore CreateStore(
        IDispatcher dispatcher,
        IEnumerable<ISlice> slices,
        IEnumerable<IEffect> effects,
        IEnumerable<IReactiveEffect> reactiveEffects)
    {
        DuckyStore store = new(dispatcher);

        store.AddSlices(slices);
        store.AddEffects(effects);
        store.AddReactiveEffects(reactiveEffects);

        return store;
    }
}
