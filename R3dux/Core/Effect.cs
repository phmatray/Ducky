using R3;
using R3dux.Exceptions;

namespace R3dux;

/// <inheritdoc />
public abstract class Effect : IEffect
{
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <inheritdoc />
    public string GetKey()
        => GetType().Name;

    /// <inheritdoc />
    public string GetAssemblyName()
        => GetType().Assembly.GetName().Name
           ?? GetType().AssemblyQualifiedName
           ?? throw new R3duxException("AssemblyQualifiedName is null.");

    public abstract Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<RootState> rootState);
}