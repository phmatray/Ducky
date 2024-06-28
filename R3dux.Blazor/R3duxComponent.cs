using Microsoft.AspNetCore.Components;
using R3;

namespace R3dux.Blazor;

public abstract class R3duxComponent<TState>
    : ComponentBase, IDisposable
{
    [Inject]
    public required Store<TState> Store { get; set; }

    private IDisposable? _stateSubscription;

    protected override void OnInitialized()
    {
        _stateSubscription = Store.State
            .DistinctUntilChanged()
            .Subscribe(_ => StateHasChanged());
    }

    protected TState State
        => GetState();

    public void Dispose()
        => _stateSubscription?.Dispose();

    protected void Dispatch(IAction action)
        => Store.Dispatch(action);
    
    private TState GetState()
    {
        var stateAsync = Store.State
            .Select(state => state)
            .FirstAsync();
        
        stateAsync.Wait();
        return stateAsync.Result;
    }
}