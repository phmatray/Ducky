using FluentAssertions;

namespace Demo.AppStore.Tests.Layout;

public class LayoutReducersTests
{
    private readonly LayoutReducers _reducers = new();

    [Fact]
    public void SetTitle_ShouldUpdateTitle()
    {
        // Arrange
        var initialState = new LayoutState
        {
            Title = "Old Title",
            Version = "1.0",
            IsModalOpen = false
        };
        
        const string newTitle = "New Title";

        // Act
        var newState = _reducers.Reduce(initialState, new SetTitle(newTitle));

        // Assert
        newState.Title.Should().Be(newTitle);
    }

    [Fact]
    public void OpenModal_ShouldSetIsModalOpenToTrue()
    {
        // Arrange
        var initialState = new LayoutState
        {
            Title = "Title",
            Version = "1.0",
            IsModalOpen = false
        };

        // Act
        var newState = _reducers.Reduce(initialState, new OpenModal());

        // Assert
        newState.IsModalOpen.Should().BeTrue();
    }

    [Fact]
    public void CloseModal_ShouldSetIsModalOpenToFalse()
    {
        // Arrange
        var initialState = new LayoutState
        {
            Title = "Title",
            Version = "1.0",
            IsModalOpen = true
        };

        // Act
        var newState = _reducers.Reduce(initialState, new CloseModal());

        // Assert
        newState.IsModalOpen.Should().BeFalse();
    }

    [Fact]
    public void SelectFullTitle_ShouldReturnCorrectFullTitle()
    {
        // Arrange
        var state = new LayoutState
        {
            Title = "Title",
            Version = "1.0",
            IsModalOpen = false
        };

        // Act
        var fullTitle = state.SelectFullTitle();

        // Assert
        fullTitle.Should().Be("Title - 1.0");
    }
}