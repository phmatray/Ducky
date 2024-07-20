// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Message;

#region State

public record MessageState
{
    public required string Message { get; init; }

    // Selectors
    public int SelectMessageLength()
    {
        return Message.Length;
    }

    public string SelectMessageInReverse()
    {
        return new string(Message.Reverse().ToArray());
    }
}

#endregion

#region Actions

public record SetMessage(string Message) : IAction;

public record AppendMessage(string Message) : IAction;

public record ClearMessage : IAction;

#endregion

#region Reducers

public record MessageReducers : SliceReducers<MessageState>
{
    public MessageReducers()
    {
        Map<SetMessage>((_, action) => new MessageState { Message = action.Message });
        Map<AppendMessage>((state, action) => new MessageState { Message = state.Message + action.Message });
        Map<ClearMessage>(() => new MessageState { Message = string.Empty });
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
