namespace Demo.AppStore;

// State
public record MessageState
{
    public string Message { get; init; } = "Hello, Blazor!";
}

// Actions
public record SetMessage(string Message) : IAction;

// Reducer
public class MessageReducer : Reducer<MessageState>
{
    public override MessageState ReduceAction(MessageState state, IAction action)
    {
        return action switch
        {
            SetMessage setMessage => new MessageState { Message = setMessage.Message },
            _ => state
        };
    }
}
