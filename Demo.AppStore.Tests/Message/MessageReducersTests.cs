using FluentAssertions;

namespace Demo.AppStore.Tests.Message;

public class MessageReducersTests
{
    private readonly MessageReducers _reducers = new();

    [Fact]
    public void SetMessage_ShouldUpdateMessage()
    {
        // Arrange
        var initialState = new MessageState { Message = "Old Message" };
        const string newMessage = "New Message";

        // Act
        var newState = _reducers.Reduce(initialState, new SetMessage(newMessage));

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
}