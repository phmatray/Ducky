// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Core;

public sealed class DispatcherTests : IDisposable
{
    private readonly Dispatcher _sut = new();
    private readonly TestActionWithParameter _action1 = new("Action1");
    private readonly TestActionWithParameter _action2 = new("Action2");
    private readonly TestActionWithParameter _action3 = new("Action3");
    private bool _disposed;

    [Fact]
    public void Dispatch_Should_EnqueueAction()
    {
        // Arrange
        var action = new TestActionWithParameter("Test");

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
    public void Dispatch_MultipleConcurrentActions_Should_EmitInOrder()
    {
        // Verifies that actions dispatched concurrently are still emitted in the order they were enqueued.
        // Arrange
        var emittedActions = new List<object>();
        _sut.ActionStream.Subscribe(emittedActions.Add);

        // Act
        Parallel.Invoke(
            () => _sut.Dispatch(_action1),
            () => _sut.Dispatch(_action2),
            () => _sut.Dispatch(_action3));

        // Assert
        emittedActions.Should().Contain([_action1, _action2, _action3]);
    }

    [Fact]
    public void UnsubscribingFromActionStream_Should_NotReceiveFurtherActions()
    {
        // Tests that unsubscribing from the ActionStream stops receiving further actions.
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
    public void Dispatch_Should_NotBlockWhenNoSubscribers()
    {
        // Ensures that the Dispatch method does not block or throw an exception
        // when there are no subscribers to the ActionStream.
        // Act
        Action act = () => _sut.Dispatch(_action1);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ActionStream_Should_CompleteWhenDispatcherDisposed()
    {
        // Verifies that the ActionStream completes when the Dispatcher is disposed,
        // ensuring proper resource cleanup.
        // Arrange
        var completed = false;
        _sut.ActionStream.Subscribe(_ => { }, _ => completed = true);

        // Act
        _sut.Dispose();

        // Assert
        completed.Should().BeTrue();
    }

    [Fact]
    public void Dispatch_Should_ThrowDuckyException_AfterDispose()
    {
        // Arrange
        _sut.Dispose();

        // Act
        Action act = () => _sut.Dispatch(_action1);

        // Assert
        act.Should().Throw<DuckyException>().WithInnerException<ObjectDisposedException>();
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

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _sut.Dispose();
            }

            _disposed = true;
        }
    }
}
