using R3dux.Temp;

namespace R3dux;

public interface ISlice
{
    string Key { get; }
    object InitialState { get; }
    Type StateType { get; }
    object Reduce(object state, IAction action);
}

public interface ISlice<TState> : ISlice
{
    new TState InitialState { get; }
    IReducer<TState> Reducers { get; }
}