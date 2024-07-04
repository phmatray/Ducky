namespace R3dux;

public abstract record Slice<TState> : ISlice<TState>
{
    public abstract string Key { get; }
    public abstract TState InitialState { get; }
    public abstract IReducer<TState> Reducers { get; }
    
    public virtual Type StateType => typeof(TState);

    // Explicit implementation of non-generic ISlice interface
    object ISlice.InitialState => InitialState!;
}