using System.Globalization;
using Ducky;
using Ducky.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace StateContainerSample.After;

#region after-component
// Ducky 2.0: the component selects what it shows and calls the SliceStore's methods. DuckyComponent marshals its own
// re-render to the renderer and disposes its selections: no OnChange, no InvokeAsync, no IDisposable.
public sealed class CounterView : DuckyComponent
{
    private Selection<CounterState>? _counter;

    [Inject]
    private CounterStore Counter { get; set; } = null!;

    public void Increment() => Counter.Increment(1);

    public void Refresh() => Dispatch(new LoadCount());

    protected override void OnInitialized() => _counter = Select((CounterState state) => state);

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, _counter!.Value.IsLoading ? "loading" : _counter.Value.Count.ToString(CultureInfo.InvariantCulture));
}
#endregion

// Selects IsLoading only: a change of Count does not re-render it.
public sealed class LoadingBadge : DuckyComponent
{
    private Selection<bool>? _isLoading;

    public int Renders { get; private set; }

    protected override void OnInitialized() => _isLoading = Select((CounterState state) => state.IsLoading);

    protected override void OnAfterRender(bool firstRender) => Renders++;

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, _isLoading!.Value ? "busy" : "idle");
}
