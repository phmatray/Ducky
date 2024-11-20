// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;

namespace Ducky;

/// <summary>
/// A dispatcher that queues and dispatches actions, providing an observable stream of dispatched actions.
/// </summary>
public sealed class Dispatcher : IDispatcher, IDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _syncRoot = new();
#else
    private readonly object _syncRoot = new();
#endif

    private readonly Queue<IAction> _queuedActions = [];
    private readonly Subject<IAction> _actionSubject = new();
    private volatile bool _isDequeuing;
    private bool _disposed;

    /// <inheritdoc />
    public Observable<IAction> ActionStream
        => _actionSubject.AsObservable();

    /// <inheritdoc />
    public void Dispatch(IAction action)
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
        _actionSubject.OnCompleted();
        _actionSubject.Dispose();
    }

    /// <summary>
    /// Dequeues and dispatches actions to the observable stream.
    /// </summary>
    private void DequeueActions()
    {
        lock (_syncRoot)
        {
            if (_isDequeuing || _actionSubject == null!)
            {
                return;
            }

            _isDequeuing = true;
        }

        while (true)
        {
            IAction dequeuedAction;

            lock (_syncRoot)
            {
                if (_queuedActions.Count == 0)
                {
                    _isDequeuing = false;
                    return;
                }

                dequeuedAction = _queuedActions.Dequeue();
            }

            _actionSubject.OnNext(dequeuedAction);
        }
    }
}
