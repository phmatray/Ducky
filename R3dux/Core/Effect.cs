using R3;
using R3dux.Temp;

namespace R3dux;

/// <inheritdoc />
public abstract class Effect : IEffect
{
    /// <inheritdoc />
    public abstract Observable<IAction> Handle(Observable<IAction> actions, Store store);
}