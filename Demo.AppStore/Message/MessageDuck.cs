namespace Demo.AppStore;

// State
public record MessageState
{
    public string Message { get; init; } = "Hello, Blazor!";
    
    // Selectors
    public int SelectMessageLength()
        => Message.Length;
    
    public string SelectMessageInReverse() 
        => new(Message.Reverse().ToArray());
}

// Actions
public record SetMessage(string Message);

// Reducers
public class MessageReducers : ReducerCollection<MessageState>
{
    public MessageReducers()
    {
        Map<SetMessage>((state, action)
            => new MessageState { Message = action.Message });
    }

    // private void Map<T>(Func<object, T, object> reduceFx)
    // {
    // }
}
