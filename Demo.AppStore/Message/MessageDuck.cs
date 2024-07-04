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
    public override string Key => "message";
    public override MessageState InitialState { get; } = new();
    public override IReducer<MessageState> Reducers { get; } = new MessageReducers();
}
