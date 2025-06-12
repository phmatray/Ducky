// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Middlewares.ReactiveEffects;

/// <summary>
/// Represents an effect that handles a stream of actions and interacts with the store's state.
/// </summary>
public abstract class ReactiveEffect
{
    /// <summary>
    /// Gets or sets the time provider used to provide the current time.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets the key that identifies the effect.
    /// </summary>
    /// <returns>The key that identifies the effect.</returns>
    public string GetKey()
    {
        return GetType().Name;
    }

    /// <summary>
    /// Gets the assembly-qualified name of the effect.
    /// </summary>
    /// <returns>The assembly-qualified name of the effect.</returns>
    public string GetAssemblyName()
    {
        return GetType().Assembly.GetName().Name
               ?? GetType().AssemblyQualifiedName
               ?? throw new DuckyException("AssemblyQualifiedName is null.");
    }

    /// <summary>
    /// Handles a stream of actions and produces a stream of resulting actions.
    /// </summary>
    /// <param name="actions">The source observable sequence of actions.</param>
    /// <param name="rootState">The source observable sequence of the root state.</param>
    /// <returns>An observable sequence of resulting actions.</returns>
    public virtual IObservable<object> Handle(
        IObservable<object> actions,
        IObservable<IRootState> rootState)
    {
        return Observable.Empty<object>();
    }
}
