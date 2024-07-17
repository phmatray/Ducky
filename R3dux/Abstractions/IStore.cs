using R3;

namespace R3dux;

public interface IStore
{
    IDispatcher Dispatcher { get; }
    Observable<RootState> RootStateObservable { get; }

    TState GetState<TState>(string key)
        where TState : notnull, new();
    
    void AddSlice(ISlice slice);
    void AddSlices(params ISlice[] slices);
    
    void AddEffect(IEffect effect);
    void AddEffects(params IEffect[] effects);
}