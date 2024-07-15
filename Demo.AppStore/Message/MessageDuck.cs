namespace Demo.AppStore;

#region State

public record MessageState
{
    public required string Message { get; init; }
    
    // Selectors
    public int SelectMessageLength()
        => Message.Length;
    
    public string SelectMessageInReverse() 
        => new(Message.Reverse().ToArray());
}

#endregion

#region Actions

public record SetMessage(string Message) : IAction;

#endregion

#region Reducers

public record MessageReducers : SliceReducers<MessageState>
{
    public MessageReducers()
    {
        Map<SetMessage>((_, action) => new MessageState { Message = action.Message });
    }

    public override MessageState GetInitialState()
    {
        return new MessageState
        {
            Message = "Hello, Blazor!"
        };
    }
}

#endregion
