using Microsoft.AspNetCore.Components;
using R3;

namespace R3dux.Blazor;

public abstract class R3duxComponent<TState>
    : ComponentBase, IDisposable
    where TState : notnull
{
    [Inject]
    public required IStore Store { get; set; }
    
    [Inject]
    public required IDispatcher Dispatcher { get; set; }

    private IDisposable? _stateSubscription;

    protected override void OnInitialized()
    {
        _stateSubscription = Store.RootState
            // .Select(state => state.Select<TState>("layout"))
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
            .Select(state => state.Select<TState>())
            .FirstAsync();
        
        stateAsync.Wait();
        return stateAsync.Result;
    }
}