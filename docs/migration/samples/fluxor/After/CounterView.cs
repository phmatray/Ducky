using System.Globalization;
using Ducky;
using Ducky.Blazor;
using Microsoft.AspNetCore.Components.Rendering;

namespace FluxorSample.After;

#region after-component
// Ducky 2.0: DuckyComponent re-renders only when a value it selected in OnInitialized changed; no base call to remember.
public sealed class CounterView : DuckyComponent
{
    private Selection<CounterState>? _counter;

    public void Increment() => Dispatch(new IncrementCounterAction(1));

    // Awaitable: completes once the action is reduced, with how it ended.
    public Task<DispatchResult> RefreshAsync() => DispatchAsync(new LoadCounterAction());

    protected override void OnInitialized() => _counter = Select((CounterState state) => state);

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, _counter!.Value.IsLoading ? "loading" : _counter.Value.Count.ToString(CultureInfo.InvariantCulture));
}
#endregion

#region after-selection
// Ducky 2.0: Select replaces the injected IStateSelection; the component re-renders when IsLoading changes, not on every
// change of the slice.
public sealed class LoadingBadge : DuckyComponent
{
    private Selection<bool>? _isLoading;

    protected override void OnInitialized() => _isLoading = Select((CounterState state) => state.IsLoading);

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, _isLoading!.Value ? "busy" : "idle");
}
#endregion
