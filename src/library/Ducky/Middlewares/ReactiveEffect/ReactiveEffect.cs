// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;

namespace Ducky.Middlewares.ReactiveEffect;

/// <inheritdoc />
public abstract class ReactiveEffect : IReactiveEffect
{
    /// <summary>
    /// Gets or init the time provider used to provide the current time.
    /// </summary>
    protected TimeProvider TimeProvider => ObservableSystem.DefaultTimeProvider;

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
            ?? throw new DuckyException("AssemblyQualifiedName is null.");
    }

    /// <inheritdoc />
    public virtual Observable<object> Handle(
        Observable<object> actions,
        Observable<IRootState> rootState)
    {
        return Observable.Empty<object>();
    }
}
