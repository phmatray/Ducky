using Microsoft.AspNetCore.Components;
using R3;

namespace R3dux.Blazor;

public abstract class R3duxComponent<TState>
    : ComponentBase, IDisposable
    where TState : notnull
{
    private IDisposable? _stateSubscription;
    
    [Inject]
    public required IStore Store { get; set; }
    
    [Inject]
    public required IDispatcher Dispatcher { get; set; }

    protected TState State { get; private set; } = default!;

    protected void Dispatch(IAction action)
        => Dispatcher.Dispatch(action);

    protected override void OnInitialized()
    {
        _stateSubscription = Store.RootState
            .Select(state => state.Select<TState>())
            .DistinctUntilChanged()
            .Subscribe(sliceState =>
            {
                State = sliceState;
                StateHasChanged();
            });
    }

    public void Dispose()
        => _stateSubscription?.Dispose();
}