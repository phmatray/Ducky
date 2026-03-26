// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
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
    public void Dispatch_Should_FireActionDispatched()
    {
        // Arrange
        TestActionWithParameter action = new("Test");
        object? dispatchedAction = null;
        _sut.ActionDispatched += (_, args) => dispatchedAction = args.Action;

        // Act
        _sut.Dispatch(action);

        // Assert
        dispatchedAction.ShouldBe(action);
    }

    [Fact]
    public void ActionDispatched_Should_EmitDispatchedActions()
    {
        // Arrange
        List<object> emittedActions = [];
        _sut.ActionDispatched += (_, args) => emittedActions.Add(args.Action);

        // Act
        _sut.Dispatch(_action1);
        _sut.Dispatch(_action2);

        // Assert
        List<object> expected = [_action1, _action2];
        foreach (object action in expected)
        {
            emittedActions.ShouldContain(action);
        }
    }

    [Fact]
    public void Dispatch_NullAction_Should_Throw_ArgumentNullException()
    {
        // Act
        Action act = () => _sut.Dispatch(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Dispatch_ConcurrentActions_Should_EmitAll()
    {
        // Arrange
        List<object> emittedActions = [];
        _sut.ActionDispatched += (_, args) =>
        {
            lock (emittedActions)
            {
                emittedActions.Add(args.Action);
            }
        };

        // Act
        Parallel.Invoke(
            () => _sut.Dispatch(_action1),
            () => _sut.Dispatch(_action2),
            () => _sut.Dispatch(_action3));

        // Assert
        List<object> expected = [_action1, _action2, _action3];
        foreach (object action in expected)
        {
            emittedActions.ShouldContain(action);
        }
    }

    [Fact]
    public void UnsubscribingFromActionDispatched_Should_NotReceiveFurtherActions()
    {
        // Arrange
        List<object> emittedActions = [];
        EventHandler<ActionDispatchedEventArgs> handler = (_, args) => emittedActions.Add(args.Action);
        _sut.ActionDispatched += handler;

        // Act
        _sut.Dispatch(_action1);
        _sut.ActionDispatched -= handler;
        _sut.Dispatch(_action2);

        // Assert
        emittedActions.ShouldHaveSingleItem().ShouldBe(_action1);
    }

    [Fact]
    public void Dispatch_Should_NotBlockWhenNoSubscribers()
    {
        // Act
        Action act = () => _sut.Dispatch(_action1);

        // Assert
        act.ShouldNotThrow();
    }

    [Fact]
    public void ActionDispatched_Should_BeNullWhenDispatcherDisposed()
    {
        // Arrange
        var eventFired = false;
        _sut.ActionDispatched += (_, _) => eventFired = true;

        // Act
        _sut.Dispose();

        // Try to dispatch after dispose (should throw)
        try
        {
            _sut.Dispatch(_action1);
        }
        catch (DuckyException)
        {
            // Expected
        }

        // Assert - event should not have fired because dispatch threw
        eventFired.ShouldBeFalse();
    }

    [Fact]
    public void Dispatch_Should_ThrowDuckyException_AfterDispose()
    {
        // Arrange
        _sut.Dispose();

        // Act
        Action act = () => _sut.Dispatch(_action1);

        // Assert
        act.ShouldThrow<DuckyException>().InnerException.ShouldBeOfType<ObjectDisposedException>();
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
        act.ShouldNotThrow();
    }

    [Fact]
    public void Dispatch_Should_SetLastAction()
    {
        // Act
        _sut.Dispatch(_action1);

        // Assert
        _sut.LastAction.ShouldBe(_action1);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sut.Dispose();
        }

        _disposed = true;
    }
}
