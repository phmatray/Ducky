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
public record AppendMessage(string Message) : IAction;
public record ClearMessage() : IAction;

#endregion

#region Reducers

public record MessageReducers : SliceReducers<MessageState>
{
    public MessageReducers()
    {
        Map<SetMessage>((_, action) => new MessageState { Message = action.Message });
        Map<AppendMessage>((state, action) => new MessageState { Message = state.Message + action.Message });
        Map<ClearMessage>((state, action) => new MessageState { Message = string.Empty });
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
