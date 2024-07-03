using R3;

namespace R3dux;

public abstract class Effect : IEffect
{
    public abstract Observable<object> Handle(
        Observable<object> actions,
        Store store);
}