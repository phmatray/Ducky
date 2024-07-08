namespace Demo.AppStore;

// State
public record MessageState
{
    public required string Message { get; init; }
    
    // Selectors
    public int SelectMessageLength()
        => Message.Length;
    
    public string SelectMessageInReverse() 
        => new(Message.Reverse().ToArray());
}

// Actions
public record SetMessage(string Message) : IAction;

// Reducers
public class MessageReducers : ReducerCollection<MessageState>
{
    public MessageReducers()
    {
        Map<SetMessage>((state, action)
            => new MessageState { Message = action.Message });
    }
}

// Slice
// ReSharper disable once UnusedType.Global
public record MessageSlice : Slice<MessageState>
{
    public override ReducerCollection<MessageState> Reducers { get; } = new MessageReducers();
    
    public override string GetKey() => "message";

    public override MessageState GetInitialState() => new()
    {
        Message = "Hello, Blazor!"
    };
}
