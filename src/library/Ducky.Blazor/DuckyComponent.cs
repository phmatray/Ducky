// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using R3;

namespace Ducky.Blazor;

/// <summary>
/// A base component class for Ducky components that manages state and dispatches actions.
/// </summary>
/// <typeparam name="TState">The type of the state managed by this component.</typeparam>
public abstract class DuckyComponent<TState>
    : ComponentBase, IDisposable
    where TState : notnull
{
    private IDisposable? _subscription;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the store that manages the application state.
    /// </summary>
    [Inject]
    public required DuckyStore Store { get; set; }

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
            var stateAsync = StateObservable.FirstAsync();
            stateAsync.Wait();
            return stateAsync.Result;
        }
    }

    /// <summary>
    /// Gets an observable stream of the state managed by this component.
    /// </summary>
    private Observable<TState> StateObservable
        => typeof(TState) == typeof(RootState)
            ? Store.RootStateObservable
                .Cast<IRootState, TState>()
            : Store.RootStateObservable
                .Select(state => state.GetSliceState<TState>())
                .DistinctUntilChanged();

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
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources.
                _subscription?.Dispose();
            }

            // Note disposing has been done.
            _disposed = true;
        }
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

        if (_subscription == null)
        {
            Logger.SubscribingToStateObservable(ComponentName);
            _subscription = StateObservable.Subscribe(OnNext);
            OnAfterSubscribed();
        }
        else
        {
            Logger.SubscriptionAlreadyAssigned(ComponentName);
        }

        Logger.ComponentInitialized(ComponentName);
    }

    /// <summary>
    /// Dispatches an action to the store.
    /// </summary>
    /// <param name="action">The action to dispatch.</param>
    protected void Dispatch(IAction action)
    {
        Dispatcher.Dispatch(action);
    }

    private void OnNext(TState state)
    {
        InvokeAsync(StateHasChanged);

        OnParametersSet();
        OnParametersSetAsync();

        Logger.ComponentRefreshed(ComponentName);
    }
}
