using R3;

namespace R3dux;

/// <inheritdoc />
public abstract class Effect : IEffect
{
    /// <inheritdoc />
    public abstract Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<RootState> rootState);
}