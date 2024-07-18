using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using R3;

namespace R3dux.Blazor;

public abstract class R3duxComponent<TState>
    : ComponentBase, IDisposable
    where TState : notnull
{
    private IDisposable? _subscription; 
    
    [Inject]
    public required IStore Store { get; set; }
    
    [Inject]
    public required ILogger<R3duxComponent<object>> Logger { get; set; }

    protected Observable<TState> StateObservable
        => Store
            .RootStateObservable
            .Select(state => state.GetSliceState<TState>())
            .DistinctUntilChanged();

    protected TState State
        => StateObservable.FirstSync();
    
    protected string ComponentName
        => GetType().Name;

    protected override void OnInitialized()
    {
        Logger.ComponentInitializing(ComponentName);

        base.OnInitialized();

        if (_subscription == null)
        {
            Logger.SubscribingToStateObservable(ComponentName);

            _subscription = StateObservable
                .Subscribe(_ =>
                {
                    InvokeAsync(StateHasChanged);
                    Logger.ComponentRefreshed(ComponentName);
                });
        }
        else
        {
            Logger.SubscriptionAlreadyAssigned(ComponentName);
        }
        
        Logger.ComponentInitialized(ComponentName);
    }

    protected void Dispatch(IAction action)
        => Store.Dispatcher.Dispatch(action);

    public void Dispose()
    {
        Logger.DisposingComponent(ComponentName);
        _subscription?.Dispose();
    }
}