namespace BlazorAppRxStore.Store;

// State
public record MessageState
{
    public string Message { get; init; } = "Hello, Blazor!";
}

// Actions
public record SetMessage(string Message) : IAction;

// Reducer
public class MessageReducer : ReducerBase<MessageState>
{
    public MessageReducer()
    {
        Register<SetMessage>((state, action)
            => new MessageState { Message = action.Message });
    }
}