namespace BlazorStore;

public class StateActionPair<TState>
{
    public required TState State { get; init; }
    public IAction? Action { get; init; }
}