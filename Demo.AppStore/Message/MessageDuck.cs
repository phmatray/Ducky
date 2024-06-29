namespace Demo.AppStore;

// State
public record MessageState
{
    public string Message { get; init; } = "Hello, Blazor!";
    
    // Selectors
    public int SelectMessageLength()
        => Message.Length;
}

// Actions
public record SetMessage(string Message) : IAction;

// Reducer
public class MessageReducer : Reducer<MessageState>
{
    public override MessageState Reduce(MessageState state, IAction action)
    {
        return action switch
        {
            SetMessage setMessage => new MessageState { Message = setMessage.Message },
            _ => state
        };
    }
}
