using FluentAssertions;

namespace Demo.AppStore.Tests.Layout;

public class LayoutReducersTests
{
    private readonly LayoutReducers _sut = new();

    private readonly LayoutState _initialState = new()
    {
        Title = "R3dux",
        Version = "v1.0.0",
        IsModalOpen = false
    };
    
    private const string Key = "layout";
    
    [Fact]
    public void LayoutReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }
    
    [Fact]
    public void LayoutReducers_Should_Return_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void SetTitle_ShouldUpdateTitle()
    {
        // Arrange
        const string newTitle = "New Title";

        // Act
        var newState = _sut.Reduce(_initialState, new SetTitle(newTitle));

        // Assert
        newState.Title.Should().Be(newTitle);
    }

    [Fact]
    public void OpenModal_ShouldSetIsModalOpenToTrue()
    {
        // Arrange
        var initialState = _initialState with { IsModalOpen = false };
        
        // Act
        var newState = _sut.Reduce(initialState, new OpenModal());

        // Assert
        newState.IsModalOpen.Should().BeTrue();
    }

    [Fact]
    public void CloseModal_ShouldSetIsModalOpenToFalse()
    {
        // Arrange
        var initialState = _initialState with { IsModalOpen = true };

        // Act
        var newState = _sut.Reduce(initialState, new CloseModal());

        // Assert
        newState.IsModalOpen.Should().BeFalse();
    }

    [Fact]
    public void SelectFullTitle_ShouldReturnCorrectFullTitle()
    {
        // Act
        var fullTitle = _initialState.SelectFullTitle();

        // Assert
        fullTitle.Should().Be("R3dux - v1.0.0");
    }
}