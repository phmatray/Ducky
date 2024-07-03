namespace R3dux;

public record Slice<TState> : ISlice<TState>
{
    public required string Key { get; init; }
    public required TState InitialState { get; init; }
    public required IReducer<TState> Reducers { get; init; }
    public IEffect[] Effects { get; init; } = [];

    // Explicit implementation of non-generic ISlice interface
    object ISlice.InitialState => InitialState!;
    IReducer<object> ISlice.Reducers => (IReducer<object>)Reducers;
}