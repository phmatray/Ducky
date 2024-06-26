namespace BlazorAppRxStore.Store;

// State
public record MessageState
{
    public string Message { get; init; } = "Hello, Blazor!";
}

// Actions
public record SetMessage(string Message) : IAction;

// Reducer
public class MessageReducer : ActionReducer<MessageState>
{
    public MessageReducer()
    {
        Register<SetMessage>((_, action)
            => new MessageState { Message = action.Message });
    }
}

public class MessageReducerFactory : IActionReducerFactory<MessageState>
{
    public IActionReducer<MessageState> CreateReducer(
        IDictionary<string, IActionReducer<MessageState>> reducers,
        MessageState initialState)
    {
        return new MessageReducer();
    }
}