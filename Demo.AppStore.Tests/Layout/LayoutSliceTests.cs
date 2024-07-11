using FluentAssertions;

namespace Demo.AppStore.Tests.Layout;

public class LayoutSliceTests
{
    private readonly LayoutSlice _sut = new();
    
    [Fact]
    public void LayoutSlice_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(LayoutState));
    }
    
    [Fact]
    public void LayoutSlice_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Reducers.Should().HaveCount(3);
    }
}