using System.Collections.Immutable;
using FluentAssertions;

namespace Demo.AppStore.Tests.Movies;

public class MovieSliceTests
{
    private readonly MovieSlice _sut = new();
    
    [Fact]
    public void MovieSlice_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(MovieState));
    }
    
    [Fact]
    public void MovieSlice_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Reducers.Should().HaveCount(3);
    }
}