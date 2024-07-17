using R3;

namespace R3dux;

public interface IStore
{
    IDispatcher Dispatcher { get; }
    Observable<RootState> RootStateObservable { get; }

    void AddSlice(ISlice slice);
    void AddSlices(params ISlice[] slices);
    
    void AddEffect(IEffect effect);
    void AddEffects(params IEffect[] effects);
}