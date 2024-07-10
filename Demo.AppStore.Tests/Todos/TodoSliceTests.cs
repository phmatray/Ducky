using FluentAssertions;

namespace Demo.AppStore.Tests.Todos;

public class TodoSliceTests
{
    private readonly TodoSlice _sut = new();
    
    [Fact]
    public void TodoSlice_Should_Return_Correct_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be("todos");
    }

    [Fact]
    public void TodoSlice_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Todos.Should().HaveCount(5);
        
        initialState.Todos[0].Title.Should().Be("Learn Blazor");
        initialState.Todos[0].IsCompleted.Should().BeTrue();
        
        initialState.Todos[1].Title.Should().Be("Learn Redux");
        initialState.Todos[1].IsCompleted.Should().BeFalse();
        
        initialState.Todos[2].Title.Should().Be("Learn Reactive Programming");
        initialState.Todos[2].IsCompleted.Should().BeFalse();
        
        initialState.Todos[3].Title.Should().Be("Create a Todo App");
        initialState.Todos[3].IsCompleted.Should().BeTrue();
        
        initialState.Todos[4].Title.Should().Be("Publish a NuGet package");
        initialState.Todos[4].IsCompleted.Should().BeFalse();
    }

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