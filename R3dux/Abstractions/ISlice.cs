namespace R3dux;

public interface ISlice
{
    string Key { get; }
    object InitialState { get; }
    IReducer<object> Reducers { get; }
    IEffect[] Effects { get; }
}

public interface ISlice<TState> : ISlice
{
    new TState InitialState { get; }
    new IReducer<TState> Reducers { get; }
}