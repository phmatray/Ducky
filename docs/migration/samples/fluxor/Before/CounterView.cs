using System.Globalization;
using Fluxor;
using Fluxor.Blazor.Web;
using Fluxor.Blazor.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FluxorSample.Before;

#region before-component
// Fluxor: FluxorComponent finds its IState<T> properties by reflection and re-renders on every change of the feature.
public sealed class CounterView : FluxorComponent
{
    [Inject]
    private IState<CounterState> CounterState { get; set; } = null!;

    [Inject]
    private IDispatcher Dispatcher { get; set; } = null!;

    public void Increment() => Dispatcher.Dispatch(new IncrementCounterAction(1));

    protected override void OnInitialized()
    {
        base.OnInitialized(); // required: FluxorComponent subscribes here (analyzer FLXW01)
        Dispatcher.Dispatch(new LoadCounterAction()); // fire-and-forget: void, no result to await
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, CounterState.Value.IsLoading ? "loading" : CounterState.Value.Count.ToString(CultureInfo.InvariantCulture));
}
#endregion

#region before-selection
// Fluxor: an injected IStateSelection narrows the re-renders to one selected value.
public sealed class LoadingBadge : FluxorComponent
{
    [Inject]
    private IStateSelection<CounterState, bool> IsLoading { get; set; } = null!;

    protected override void OnInitialized()
    {
        IsLoading.Select(state => state.IsLoading);
        base.OnInitialized();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, IsLoading.Value ? "busy" : "idle");
}
#endregion

#region before-initializer
// Fluxor: <Fluxor.Blazor.Web.StoreInitializer /> above the <Router> (in Routes.razor or App.razor) starts the store
// in its OnAfterRenderAsync. This is the same markup written in C#.
public sealed class Routes : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<StoreInitializer>(0);
        builder.CloseComponent();
        builder.AddContent(1, ChildContent);
    }
}
#endregion
