// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Blazor;

/// <summary>
/// A base component class for R3dux components that manages state and dispatches actions.
/// </summary>
/// <typeparam name="TState">The type of the state managed by this component.</typeparam>
public abstract class R3duxComponent<TState>
    : ComponentBase, IDisposable
    where TState : notnull
{
    private IDisposable? _subscription;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the store that manages the application state.
    /// </summary>
    [Inject]
    public required IStore Store { get; set; }

    /// <summary>
    /// Gets or sets the logger used for logging information.
    /// </summary>
    [Inject]
    public required ILogger<R3duxComponent<object>> Logger { get; set; }

    /// <summary>
    /// Gets an observable stream of the root state of the application.
    /// </summary>
    protected Observable<IRootState> RootStateObservable
        => Store
            .RootStateObservable
            .DistinctUntilChanged();

    /// <summary>
    /// Gets an observable stream of the state managed by this component.
    /// </summary>
    protected Observable<TState> StateObservable
        => typeof(TState) == typeof(IRootState)
            ? RootStateObservable.Cast<IRootState, TState>()
            : RootStateObservable.Select(state => state.GetSliceState<TState>());

    /// <summary>
    /// Gets the current state of the component.
    /// </summary>
    protected TState State
        => StateObservable.FirstSync();

    /// <summary>
    /// Gets the name of the component.
    /// </summary>
    protected string ComponentName
        => GetType().Name;

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
    /// Releases the unmanaged resources used by the <see cref="R3duxComponent{TState}"/> and optionally releases the managed resources.
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
        Store.Dispatcher.Dispatch(action);
    }

    private void OnNext(TState state)
    {
        InvokeAsync(StateHasChanged);

        OnParametersSet();
        OnParametersSetAsync();

        Logger.ComponentRefreshed(ComponentName);
    }
}
