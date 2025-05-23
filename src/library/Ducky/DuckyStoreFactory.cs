// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares;
using Ducky.Pipeline;

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
    /// <param name="pipelineEventPublisher">The event publisher for pipeline events.</param>
    /// <param name="slices">The collection of slices to be added to the store.</param>
    /// <param name="asyncEffects">The collection of async effects to be added to the store.</param>
    /// <param name="reactiveEffects">The collection of reactive effects to be added to the store.</param>
    /// <param name="middlewares">The collection of middlewares to be added to the store.</param>
    /// <returns>A new instance of <see cref="DuckyStore"/>.</returns>
    public static DuckyStore CreateStore(
        IDispatcher dispatcher,
        IPipelineEventPublisher pipelineEventPublisher,
        IEnumerable<ISlice> slices,
        IEnumerable<IAsyncEffect> asyncEffects,
        IEnumerable<IReactiveEffect> reactiveEffects,
        IEnumerable<IStoreMiddleware> middlewares)
    {
        DuckyStore store = new(dispatcher, pipelineEventPublisher);

        store.AddSlices(slices);
        store.AddAsyncEffects(asyncEffects);
        store.AddReactiveEffects(reactiveEffects);
        store.AddMiddlewares(middlewares);

        return store;
    }
}
