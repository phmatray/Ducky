using FluentAssertions;

namespace Demo.AppStore.Tests.Layout;

public class LayoutReducersTests
{
    private readonly LayoutReducers _sut = new();

    private readonly LayoutState _initialState = new()
    {
        Title = "R3dux",
        Version = "v1.0.0",
        IsDarkMode = true,
        IsDrawerOpen = true,
        IsNotificationOpen = false,
        Notifications = []
    };
    
    private const string Key = "layout";
    
    [Fact]
    public void LayoutReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Title.Should().BeEquivalentTo(_initialState.Title);
        initialState.Version.Should().BeEquivalentTo(_initialState.Version);
        initialState.IsDarkMode.Should().Be(_initialState.IsDarkMode);
        initialState.IsDrawerOpen.Should().Be(_initialState.IsDrawerOpen);
        initialState.IsNotificationOpen.Should().Be(_initialState.IsNotificationOpen);
        initialState.Notifications.Should().HaveCount(3);
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
    public void LayoutReducers_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(LayoutState));
    }
    
    [Fact]
    public void LayoutReducers_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(6);
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
    public void SelectFullTitle_ShouldReturnCorrectFullTitle()
    {
        // Act
        var fullTitle = _initialState.SelectFullTitle();

        // Assert
        fullTitle.Should().Be("R3dux - v1.0.0");
    }
}