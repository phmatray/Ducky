// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

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
}
