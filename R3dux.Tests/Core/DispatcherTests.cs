using R3dux.Exceptions;
using R3dux.Tests.TestModels;

namespace R3dux.Tests.Core;

public class DispatcherTests
{
    private readonly Dispatcher _sut = new();
    private readonly TestAction _action1 = new("Action1");
    private readonly TestAction _action2 = new("Action2");
    private readonly TestAction _action3 = new("Action3");
    
    [Fact]
    public void Dispatch_Should_EnqueueAction()
    {
        // Arrange
        var action = new TestAction("Test");

        // Act
        _sut.Dispatch(action);

        // Assert
        _sut.ActionStream.Should().NotBeNull();
    }

    [Fact]
    public void ActionStream_Should_EmitDispatchedActions()
    {
        // Arrange
        var emittedActions = new List<object>();
        _sut.ActionStream.Subscribe(emittedActions.Add);

        // Act
        _sut.Dispatch(_action1);
        _sut.Dispatch(_action2);

        // Assert
        emittedActions.Should().ContainInOrder(_action1, _action2);
    }

    [Fact]
    public void Dispatch_NullAction_Should_Throw_ArgumentNullException()
    {
        // Act
        Action act = () => _sut.Dispatch(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    // Verifies that actions dispatched concurrently are still emitted in the order they were enqueued.
    public void Dispatch_MultipleConcurrentActions_Should_EmitInOrder()
    {
        // Arrange
        var emittedActions = new List<object>();
        _sut.ActionStream.Subscribe(emittedActions.Add);

        // Act
        Parallel.Invoke(
            () => _sut.Dispatch(_action1),
            () => _sut.Dispatch(_action2),
            () => _sut.Dispatch(_action3)
        );

        // Assert
        emittedActions.Should().Contain(new[] { _action1, _action2, _action3 });
    }
    
    [Fact]
    // Tests that unsubscribing from the ActionStream stops receiving further actions.
    public void UnsubscribingFromActionStream_Should_NotReceiveFurtherActions()
    {
        // Arrange
        var emittedActions = new List<object>();
        var subscription = _sut.ActionStream.Subscribe(emittedActions.Add);

        // Act
        _sut.Dispatch(_action1);
        subscription.Dispose();
        _sut.Dispatch(_action2);

        // Assert
        emittedActions.Should().ContainSingle().Which.Should().Be(_action1);
    }
    
    [Fact]
    // Ensures that the Dispatch method does not block or throw an exception
    // when there are no subscribers to the ActionStream.
    public void Dispatch_Should_NotBlockWhenNoSubscribers()
    {
        // Act
        Action act = () => _sut.Dispatch(_action1);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    // Verifies that the ActionStream completes when the Dispatcher is disposed,
    // ensuring proper resource cleanup.
    public void ActionStream_Should_CompleteWhenDispatcherDisposed()
    {
        // Arrange
        var completed = false;
        _sut.ActionStream.Subscribe(_ => { }, _ => completed = true);

        // Act
        _sut.Dispose();

        // Assert
        completed.Should().BeTrue();
    }
    
    [Fact]
    public void Dispatch_Should_ThrowR3duxException_AfterDispose()
    {
        // Arrange
        _sut.Dispose();

        // Act
        Action act = () => _sut.Dispatch(_action1);

        // Assert
        act.Should().Throw<R3duxException>().WithInnerException<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_Should_NotThrowIfCalledMultipleTimes()
    {
        // Act
        Action act = () =>
        {
            _sut.Dispose();
            _sut.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }
}