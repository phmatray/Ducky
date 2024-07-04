using R3;
using R3dux.Temp;

namespace R3dux;

public abstract class Effect : IEffect
{
    public abstract Observable<IAction> Handle(
        Observable<IAction> actions,
        Store store);
}