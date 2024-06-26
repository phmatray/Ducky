namespace BlazorStore;

public class StateActionPair<TState>
{
    public TState State { get; set; }
    public IAction Action { get; set; }
}