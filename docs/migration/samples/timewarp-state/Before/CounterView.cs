using System.Globalization;
using Microsoft.AspNetCore.Components.Rendering;
using TimeWarp.State;

namespace TimeWarpSample.Before;

#region before-component
// TimeWarp.State: GetState<T>() subscribes the component to the whole state; the generated methods send the actions.
public sealed class CounterView : TimeWarpStateComponent
{
    private CounterState CounterState => GetState<CounterState>();

    public Task IncrementAsync() => CounterState.IncrementCount(amount: 1); // generated from IncrementCountActionSet

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await CounterState.LoadCount(); // generated from LoadCountActionSet
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, CounterState.IsLoading ? "loading" : CounterState.Count.ToString(CultureInfo.InvariantCulture));
}
#endregion
