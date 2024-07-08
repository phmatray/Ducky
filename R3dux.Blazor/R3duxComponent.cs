using Microsoft.AspNetCore.Components;
using R3;
using R3dux.Temp;

namespace R3dux.Blazor;

public abstract class R3duxComponent<TState>
    : ComponentBase, IDisposable
    where TState : notnull
{
    [Inject]
    public required Store Store { get; set; }
    
    [Inject]
    public required IDispatcher Dispatcher { get; set; }

    private IDisposable? _stateSubscription;

    protected override void OnInitialized()
    {
        _stateSubscription = Store.RootState
            .DistinctUntilChanged()
            .Subscribe(_ => StateHasChanged());
    }

    protected TState State
        => GetState();

    public void Dispose()
        => _stateSubscription?.Dispose();

    protected void Dispatch(IAction action)
        => Dispatcher.Dispatch(action);
    
    private TState GetState()
    {
        var stateAsync = Store.RootState
            .Select(state => state)
            .FirstAsync();
        
        stateAsync.Wait();
        // return stateAsync.Result;
        // return new TState();
        return default!;
    }
}