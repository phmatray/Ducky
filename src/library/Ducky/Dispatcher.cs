// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// A thin event emitter that validates and dispatches actions immediately.
/// </summary>
public sealed class Dispatcher : IDispatcher, IDisposable
{
    private readonly Lock _syncRoot = new();
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

    /// <inheritdoc />
    public object? LastAction { get; private set; }

    /// <inheritdoc />
    public void Dispatch<TAction>(TAction action) where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(action);
#pragma warning disable CS0618 // Obsolete Dispatch(object) – called internally by the typed overload
        Dispatch((object)action);
#pragma warning restore CS0618
    }

    /// <inheritdoc />
    public void Dispatch(object action)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                throw ExceptionFactory.DispatcherDisposed();
            }
        }

        ArgumentNullException.ThrowIfNull(action);

        LastAction = action;
        ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
    }

    /// <summary>
    /// Releases all resources used by the <see cref="Dispatcher"/> class.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ActionDispatched = null;
        }
    }
}
