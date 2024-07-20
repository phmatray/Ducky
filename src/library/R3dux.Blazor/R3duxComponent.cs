using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using R3;

namespace R3dux.Blazor;

public abstract class R3duxComponent<TState>
    : ComponentBase, IDisposable
    where TState : notnull
{
    private IDisposable? _subscription; 
    private bool _disposed;
    
    [Inject]
    public required IStore Store { get; set; }
    
    [Inject]
    public required ILogger<R3duxComponent<object>> Logger { get; set; }
    
    protected Observable<RootState> RootStateObservable
        => Store
            .RootStateObservable
            .DistinctUntilChanged();

    protected Observable<TState> StateObservable
        => typeof(TState) == typeof(RootState)
            ? RootStateObservable.Cast<RootState, TState>()
            : RootStateObservable.Select(state => state.GetSliceState<TState>());

    protected TState State
        => StateObservable.FirstSync();
    
    protected string ComponentName
        => GetType().Name;
    
    /// <summary>
    /// Invoked after the state subscription has been established.
    /// This method is intended to be overridden by derived classes.
    /// </summary>
    protected virtual void OnAfterSubscribed() { }
    
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

    protected void Dispatch(IAction action)
        => Store.Dispatcher.Dispatch(action);
    
    private void OnNext(TState state)
    {
        InvokeAsync(StateHasChanged);
                    
        OnParametersSet();
        OnParametersSetAsync();
                    
        Logger.ComponentRefreshed(ComponentName);
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
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
