// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;

namespace Ducky;

/// <summary>
/// Represents a store that manages application state and handles actions.
/// </summary>
internal interface IStore
{
    /// <summary>
    /// Gets an observable stream of the root state of the application.
    /// </summary>
    ReadOnlyReactiveProperty<IRootState> RootStateObservable { get; }

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
}
