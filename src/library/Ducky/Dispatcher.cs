// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// A dispatcher that queues and dispatches actions, providing events for dispatched actions.
/// </summary>
public sealed class Dispatcher : IDispatcher, IDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _syncRoot = new();
#else
    private readonly object _syncRoot = new();
#endif

    private readonly Queue<object> _queuedActions = [];
    private volatile bool _isDequeuing;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

    /// <inheritdoc />
    public object? LastAction { get; private set; }

    /// <inheritdoc />
    public void Dispatch(object action)
    {
        if (_disposed)
        {
            throw new DuckyException(
                "The dispatcher has been disposed.",
                new ObjectDisposedException(nameof(Dispatcher)));
        }

        ArgumentNullException.ThrowIfNull(action);

        lock (_syncRoot)
        {
            _queuedActions.Enqueue(action);
        }

        DequeueActions();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="Dispatcher"/> class.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ActionDispatched = null;
    }

    /// <summary>
    /// Dequeues and dispatches actions to event handlers.
    /// </summary>
    private void DequeueActions()
    {
        lock (_syncRoot)
        {
            if (_isDequeuing)
            {
                return;
            }

            _isDequeuing = true;
        }

        while (true)
        {
            object dequeuedAction;

            lock (_syncRoot)
            {
                if (_queuedActions.Count == 0)
                {
                    _isDequeuing = false;
                    return;
                }

                dequeuedAction = _queuedActions.Dequeue();
            }

            LastAction = dequeuedAction;
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(dequeuedAction));
        }
    }
}
