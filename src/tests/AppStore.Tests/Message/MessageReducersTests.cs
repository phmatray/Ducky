// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Tests.Message;

public sealed class MessageReducersTests : IDisposable
{
    private const string Key = "message";

    private readonly MessageReducers _sut = new();
    private readonly MessageState _initialState = new()
    {
        Message = "Hello, Blazor!"
    };

    private bool _disposed;

    [Fact]
    public void MessageReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }

    [Fact]
    public void MessageReducers_Should_Return_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void MessageReducers_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be<MessageState>();
    }

    [Fact]
    public void MessageReducers_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(3);
    }

    [Fact]
    public void SetMessage_ShouldUpdateMessage()
    {
        // Arrange
        const string newMessage = "New Message";

        // Act
        var newState = _sut.Reduce(_initialState, new SetMessage(newMessage));

        // Assert
        newState.Message.Should().Be(newMessage);
    }

    [Fact]
    public void SelectMessageLength_ShouldReturnCorrectLength()
    {
        // Arrange
        var state = new MessageState { Message = "Hello" };

        // Act
        var messageLength = state.SelectMessageLength();

        // Assert
        messageLength.Should().Be(5);
    }

    [Fact]
    public void SelectMessageInReverse_ShouldReturnMessageInReverse()
    {
        // Arrange
        var state = new MessageState { Message = "Hello" };

        // Act
        var reversedMessage = state.SelectMessageInReverse();

        // Assert
        reversedMessage.Should().Be("olleH");
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _sut.Dispose();
            }

            _disposed = true;
        }
    }
}
