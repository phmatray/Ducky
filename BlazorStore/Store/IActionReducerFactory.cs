namespace BlazorStore;

public interface IActionReducerFactory<TState>
{
    IActionReducer<TState> CreateReducer(IDictionary<string, IActionReducer<TState>> reducers, TState initialState);
}