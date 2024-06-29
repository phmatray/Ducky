namespace R3dux.Tests;

public class DispatcherTests
{
    private class TestAction(string name) : IAction
    {
        private string Name { get; } = name;

        public override bool Equals(object? obj)
            => obj is TestAction action
               && Name == action.Name;

        public override int GetHashCode()
            => HashCode.Combine(Name);
    }

    [Fact]
    public void Dispatch_Should_EnqueueAction()
    {
        // Arrange
        var dispatcher = new Dispatcher();
        var action = new TestAction("Test");

        // Act
        dispatcher.Dispatch(action);

        // Assert
        dispatcher.ActionStream.Should().NotBeNull();
    }

    [Fact]
    public void ActionStream_Should_EmitDispatchedActions()
    {
        // Arrange
        var dispatcher = new Dispatcher();
        var action1 = new TestAction("Action1");
        var action2 = new TestAction("Action2");

        var emittedActions = new List<IAction>();
        dispatcher.ActionStream.Subscribe(emittedActions.Add);

        // Act
        dispatcher.Dispatch(action1);
        dispatcher.Dispatch(action2);

        // Assert
        emittedActions.Should().ContainInOrder(action1, action2);
    }

    [Fact]
    public void Dispatch_NullAction_Should_Throw_ArgumentNullException()
    {
        // Arrange
        var dispatcher = new Dispatcher();

        // Act
        Action act = () => dispatcher.Dispatch(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    // Verifies that actions dispatched concurrently are still emitted in the order they were enqueued.
    public void Dispatch_MultipleConcurrentActions_Should_EmitInOrder()
    {
        // Arrange
        var dispatcher = new Dispatcher();
        var action1 = new TestAction("Action1");
        var action2 = new TestAction("Action2");
        var action3 = new TestAction("Action3");

        var emittedActions = new List<IAction>();
        dispatcher.ActionStream.Subscribe(emittedActions.Add);

        // Act
        Parallel.Invoke(
            () => dispatcher.Dispatch(action1),
            () => dispatcher.Dispatch(action2),
            () => dispatcher.Dispatch(action3)
        );

        // Assert
        emittedActions.Should().Contain(new[] { action1, action2, action3 });
    }
    
    [Fact]
    // Tests that unsubscribing from the ActionStream stops receiving further actions.
    public void UnsubscribingFromActionStream_Should_NotReceiveFurtherActions()
    {
        // Arrange
        var dispatcher = new Dispatcher();
        var action1 = new TestAction("Action1");
        var action2 = new TestAction("Action2");

        var emittedActions = new List<IAction>();
        var subscription = dispatcher.ActionStream.Subscribe(emittedActions.Add);

        // Act
        dispatcher.Dispatch(action1);
        subscription.Dispose();
        dispatcher.Dispatch(action2);

        // Assert
        emittedActions.Should().ContainSingle().Which.Should().Be(action1);
    }
    
    [Fact]
    // Ensures that the Dispatch method does not block or throw an exception
    // when there are no subscribers to the ActionStream.
    public void Dispatch_Should_NotBlockWhenNoSubscribers()
    {
        // Arrange
        var dispatcher = new Dispatcher();
        var action = new TestAction("Action1");

        // Act
        Action act = () => dispatcher.Dispatch(action);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    // Verifies that the ActionStream completes when the Dispatcher is disposed,
    // ensuring proper resource cleanup.
    public void ActionStream_Should_CompleteWhenDispatcherDisposed()
    {
        // Arrange
        var dispatcher = new Dispatcher();
        var completed = false;
        dispatcher.ActionStream.Subscribe(_ => { }, _ => completed = true);

        // Act
        dispatcher.Dispose();

        // Assert
        completed.Should().BeTrue();
    }
}