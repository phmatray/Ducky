using Microsoft.AspNetCore.Components;
using R3;

namespace R3dux.Blazor;

public abstract class R3duxComponent<TState>
    : ComponentBase, IDisposable
    where TState : notnull
{
    private readonly CompositeDisposable _disposables = [];
    
    [Inject]
    public required IStore Store { get; set; }

    protected Observable<TState> StateObservable
        => Store.RootState
            .Select(state => state.GetSliceState<TState>())
            .DistinctUntilChanged();

    protected TState State
        => StateObservable.FirstSync();

    protected override void OnInitialized()
        => StateObservable
            .Subscribe(_ => InvokeAsync(StateHasChanged))
            .AddTo(_disposables);

    protected void Dispatch(IAction action)
        => Store.Dispatch(action);

    public void Dispose()
        => _disposables.Dispose();
}