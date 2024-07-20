// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux;

/// <inheritdoc />
public abstract class Effect : IEffect
{
    /// <summary>
    /// Gets or init the time provider used to provide the current time.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <inheritdoc />
    public string GetKey()
    {
        return GetType().Name;
    }

    /// <inheritdoc />
    public string GetAssemblyName()
    {
        return GetType().Assembly.GetName().Name
               ?? GetType().AssemblyQualifiedName
               ?? throw new R3duxException("AssemblyQualifiedName is null.");
    }

    /// <inheritdoc />
    public abstract Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<IRootState> rootState);
}
