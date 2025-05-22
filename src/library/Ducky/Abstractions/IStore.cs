// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares;
using R3;

namespace Ducky;

/// <summary>
/// Represents a store that manages application state and handles actions.
/// </summary>
public interface IStore
{
    /// <summary>
    /// Gets an observable stream of the root state of the application.
    /// </summary>
    ReadOnlyReactiveProperty<IRootState> RootStateObservable { get; }

    /// <summary>
    /// Gets the current root state of the application.
    /// </summary>
    IRootState CurrentState { get; }

    /// <summary>
    /// Adds a single slice to the store.
    /// </summary>
    /// <param name="slice">The slice to add.</param>
    void AddSlice(ISlice slice);

    /// <summary>
    /// Adds multiple slices to the store.
    /// </summary>
    /// <param name="slices">The slices to add.</param>
    void AddSlices(params IEnumerable<ISlice> slices);

    /// <summary>
    /// Adds a single async effect to the store.
    /// </summary>
    /// <param name="asyncEffect">The async effect to add.</param>
    void AddAsyncEffect(IAsyncEffect asyncEffect);

    /// <summary>
    /// Adds multiple async effects to the store.
    /// </summary>
    /// <param name="asyncEffects">The async effects to add.</param>
    void AddAsyncEffects(params IEnumerable<IAsyncEffect> asyncEffects);

    /// <summary>
    /// Adds a single reactive effect to the store.
    /// </summary>
    /// <param name="reactiveEffect">The reactive effect to add.</param>
    void AddReactiveEffect(IReactiveEffect reactiveEffect);

    /// <summary>
    /// Adds multiple reactive effects to the store.
    /// </summary>
    /// <param name="reactiveEffects">The reactive effects to add.</param>
    void AddReactiveEffects(params IEnumerable<IReactiveEffect> reactiveEffects);

    /// <summary>
    /// Adds a single middleware to the store.
    /// </summary>
    /// <param name="middleware">The middleware to add.</param>
    void AddMiddleware(IStoreMiddleware middleware);

    /// <summary>
    /// Adds multiple middlewares to the store.
    /// </summary>
    /// <param name="middlewares">The middlewares to add.</param>
    void AddMiddlewares(params IEnumerable<IStoreMiddleware> middlewares);
}
