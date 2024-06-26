namespace BlazorStore.Tests;

public class ActionTests
{
    private record IncrementAction(int Amount) : IAction;

    private record DecrementAction(int Amount) : IAction;

    private record ResetAction : IAction;
    
    [Fact]
    public void IncrementAction_ShouldImplementIAction()
    {
        // Arrange
        var action = new IncrementAction(5);

        // Act & Assert
        action.Should().BeAssignableTo<IAction>();
    }

    [Fact]
    public void DecrementAction_ShouldImplementIAction()
    {
        // Arrange
        var action = new DecrementAction(3);

        // Act & Assert
        action.Should().BeAssignableTo<IAction>();
    }

    [Fact]
    public void ResetAction_ShouldImplementIAction()
    {
        // Arrange
        var action = new ResetAction();

        // Act & Assert
        action.Should().BeAssignableTo<IAction>();
    }

    [Fact]
    public void IncrementAction_TypeShouldReturnActionName()
    {
        // Arrange
        var action = new IncrementAction(5);

        // Act
        var typeName = ((IAction)action).Type;

        // Assert
        typeName.Should().Be(nameof(IncrementAction));
    }

    [Fact]
    public void DecrementAction_TypeShouldReturnActionName()
    {
        // Arrange
        var action = new DecrementAction(3);

        // Act
        var typeName = ((IAction)action).Type;

        // Assert
        typeName.Should().Be(nameof(DecrementAction));
    }

    [Fact]
    public void ResetAction_TypeShouldReturnActionName()
    {
        // Arrange
        var action = new ResetAction();

        // Act
        var typeName = ((IAction)action).Type;

        // Assert
        typeName.Should().Be(nameof(ResetAction));
    }
}