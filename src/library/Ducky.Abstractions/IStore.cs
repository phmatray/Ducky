// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;

namespace Ducky.Abstractions;

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
    void AddSlices(params ISlice[] slices);

    /// <summary>
    /// Adds a single effect to the store.
    /// </summary>
    /// <param name="effect">The effect to add.</param>
    void AddEffect(IEffect effect);

    /// <summary>
    /// Adds multiple effects to the store.
    /// </summary>
    /// <param name="effects">The effects to add.</param>
    void AddEffects(params IEffect[] effects);
}
