using FluentAssertions;

namespace Demo.AppStore.Tests.Todos;

public class TodoSliceTests
{
    private readonly TodoSlice _sut = new();

    [Fact]
    public void TodoSlice_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(TodoState));
    }
    
    [Fact]
    public void TodoSlice_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Reducers.Should().HaveCount(3);
    }
}