using FluentAssertions;

namespace Demo.AppStore.Tests.Message;

public class MessageSliceTests
{
    private readonly MessageSlice _sut = new();
    
    [Fact]
    public void MessageSlice_Should_Return_Correct_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be("message");
    }

    [Fact]
    public void MessageSlice_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(MessageState));
    }
    
    [Fact]
    public void MessageSlice_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Reducers.Should().HaveCount(1);
    }
}