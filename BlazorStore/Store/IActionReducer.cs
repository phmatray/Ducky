namespace BlazorStore;

public interface IActionReducer<TState>
{
    TState Invoke(TState state, IAction action);
}