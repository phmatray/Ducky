// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace AppStore.Tests.Message;

public sealed class MessageReducersTests : IDisposable
{
    private const string Key = "message";

    private readonly MessageReducers _sut = new();
    private readonly MessageState _initialState = new() { Message = "Hello, Blazor!" };

    private bool _disposed;

    [Fact]
    public void MessageReducers_Should_Return_Initial_State()
    {
        // Act
        MessageState initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }

    [Fact]
    public void MessageReducers_Should_Return_Key()
    {
        // Act
        string key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void MessageReducers_Should_Return_Correct_State_Type()
    {
        // Act
        Type stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be<MessageState>();
    }

    [Fact]
    public void MessageReducers_Should_Return_Reducers()
    {
        // Act
        Dictionary<Type, Func<MessageState, object, MessageState>> reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(3);
    }

    [Fact]
    public void SetMessage_ShouldUpdateMessage()
    {
        // Arrange
        const string newMessage = "New Message";

        // Act
        MessageState newState = _sut.Reduce(_initialState, new SetMessage(newMessage));

        // Assert
        newState.Message.Should().Be(newMessage);
    }

    [Fact]
    public void SelectMessageLength_ShouldReturnCorrectLength()
    {
        // Arrange
        MessageState state = new() { Message = "Hello" };

        // Act
        int messageLength = state.SelectMessageLength();

        // Assert
        messageLength.Should().Be(5);
    }

    [Fact]
    public void SelectMessageInReverse_ShouldReturnMessageInReverse()
    {
        // Arrange
        MessageState state = new() { Message = "Hello" };

        // Act
        string reversedMessage = state.SelectMessageInReverse();

        // Assert
        reversedMessage.Should().Be("olleH");
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sut.Dispose();
        }

        _disposed = true;
    }
}
