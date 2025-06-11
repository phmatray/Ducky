// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor;

/// <summary>
/// A base component class for Ducky components that manages state and dispatches actions.
/// </summary>
/// <typeparam name="TState">The type of the state managed by this component.</typeparam>
public abstract class DuckyComponent<TState> : ComponentBase, IDisposable
    where TState : notnull
{
    private TState? _currentState;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the store that manages the application state.
    /// </summary>
    [Inject]
    public required IStore Store { get; set; }

    /// <summary>
    /// Gets or sets the dispatcher used to dispatch actions to the store.
    /// </summary>
    [Inject]
    public required IDispatcher Dispatcher { get; set; }

    /// <summary>
    /// Gets or sets the logger used for logging information.
    /// </summary>
    [Inject]
    public required ILogger<DuckyComponent<object>> Logger { get; set; }

    /// <summary>
    /// Gets the name of the component.
    /// </summary>
    protected string ComponentName
        => GetType().Name;

    /// <summary>
    /// Gets the current state of the component.
    /// </summary>
    protected TState State
    {
        get
        {
            if (_currentState is null)
            {
                UpdateCurrentState();
            }

            return _currentState!;
        }
    }

    /// <summary>
    /// Updates the current state from the store.
    /// </summary>
    private void UpdateCurrentState()
    {
        if (typeof(TState) == typeof(IRootState) || typeof(TState) == typeof(RootState))
        {
            _currentState = (TState)(object)Store.CurrentState;
        }
        else
        {
            _currentState = Store.CurrentState.GetSliceState<TState>();
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="DuckyComponent{TState}"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">If true, the method has been called directly or indirectly by a user's code. Managed and unmanaged resources can be disposed. If false, the method has been called by the runtime from inside the finalizer and only unmanaged resources can be disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources.
            Store.StateChanged -= OnStateChanged;
        }

        // Note disposing has been done.
        _disposed = true;
    }

    /// <summary>
    /// Invoked after the state subscription has been established.
    /// This method is intended to be overridden by derived classes.
    /// </summary>
    protected virtual void OnAfterSubscribed()
    {
    }

    /// <inheritdoc />
    protected sealed override void OnInitialized()
    {
        Logger.ComponentInitializing(ComponentName);

        base.OnInitialized();

        // Subscribe to state changes
        Store.StateChanged += OnStateChanged;
        OnAfterSubscribed();

        // Initialize the current state
        UpdateCurrentState();

        Logger.ComponentInitialized(ComponentName);
    }

    /// <summary>
    /// Dispatches an action to the store.
    /// </summary>
    /// <param name="action">The action to dispatch.</param>
    protected void Dispatch(object action)
    {
        Dispatcher.Dispatch(action);
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        TState? previousState = _currentState;
        UpdateCurrentState();

        // Only re-render if the state actually changed
        if (EqualityComparer<TState>.Default.Equals(previousState, _currentState))
        {
            return;
        }

        InvokeAsync(StateHasChanged);
        Logger.ComponentRefreshed(ComponentName);
    }
}
