namespace BlazorAppRxStore.Store;

// State
public record AppState
{
    public int Count { get; init; } = 0;
    public string Message { get; init; } = "Hello, Blazor!";
}

// Actions
public record SetMessage(string Message) : IAction;

// Reducer
public class AppReducer : ReducerBase<AppState>
{
    public AppReducer()
    {
        Register<SetMessage>((state, action)
            => state with { Message = action.Message });
    }
}