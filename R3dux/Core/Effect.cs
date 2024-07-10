using R3;

namespace R3dux;

/// <inheritdoc />
public abstract class Effect : IEffect
{
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <inheritdoc />
    public abstract Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<RootState> rootState);
}