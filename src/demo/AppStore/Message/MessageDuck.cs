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
        => Message.Length;

    public string SelectMessageInReverse()
        => string.Concat(Message.Reverse());

    public string SelectMessageInUpperCase()
        => Message.ToUpper(CultureInfo.InvariantCulture);
}

#endregion

#region Actions

public record SetMessage(string Message);

public record AppendMessage(string Message);

public record ClearMessage;

#endregion

#region Reducers

public record MessageReducers : SliceReducers<MessageState>
{
    public MessageReducers()
    {
        On<SetMessage>(Reduce);
        On<AppendMessage>(Reduce);
        On<ClearMessage>(Reduce);
    }

    public override MessageState GetInitialState()
        => new() { Message = "Hello, Blazor!" };

    private static MessageState Reduce(MessageState _, SetMessage action)
        => new() { Message = action.Message };

    private static MessageState Reduce(MessageState state, AppendMessage action)
        => new() { Message = state.Message + action.Message };

    private static MessageState Reduce(MessageState state, ClearMessage action)
        => new() { Message = string.Empty };
}

#endregion
