using System.Globalization;
using Ducky;
using Ducky.Blazor;
using Microsoft.AspNetCore.Components.Rendering;

namespace TimeWarpSample.After;

#region after-component
// Ducky 2.0: DuckyComponent<TState>'s State is a selection of the whole slice; the generated helpers dispatch.
public sealed class CounterView : DuckyComponent<CounterState>
{
    public Task<DispatchResult> IncrementAsync() => Store.IncrementCountAsync(1); // completes once reduced

    protected override async Task OnInitializedAsync() =>
        await Store.LoadCountAsync(); // completes once LoadCount is reduced, not once the count arrived

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, State.IsLoading ? "loading" : State.Count.ToString(CultureInfo.InvariantCulture));
}
#endregion
