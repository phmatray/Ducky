using R3dux.Temp;

namespace R3dux;

public abstract record Slice<TState>
    : ISlice<TState>
{
    public abstract string Key { get; }
    public abstract TState InitialState { get; }
    public abstract ReducerCollection<TState> Reducers { get; }
    
    public virtual Type StateType => typeof(TState);

    // Explicit implementation of non-generic ISlice interface
    object ISlice.InitialState => InitialState!;

    public object Reduce(object state, IAction action)
    {
        return Reducers.Reduce((TState)state, action);
    }
}