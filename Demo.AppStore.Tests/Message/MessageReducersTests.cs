using FluentAssertions;

namespace Demo.AppStore.Tests.Message;

public class MessageReducersTests
{
    private readonly MessageReducers _sut = new();

    private readonly MessageState _initialState = new()
    {
        Message = "Hello, Blazor!"
    };
    
    private const string Key = "message";

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
        stateType.Should().Be(typeof(MessageState));
    }
    
    [Fact]
    public void MessageReducers_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(1);
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
}