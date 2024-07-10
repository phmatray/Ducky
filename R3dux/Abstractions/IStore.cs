using R3;

namespace R3dux;

public interface IStore
{
    Observable<RootState> RootStateObservable { get; }

    TState GetState<TState>(string key)
        where TState : notnull, new();
    
    void Dispatch(IAction action);
    
    void AddSlice(ISlice slice);
    void AddSlices(IEnumerable<ISlice> slices);
    
    void AddEffect(IEffect effect);
    void AddEffects(IEnumerable<IEffect> effects);
}