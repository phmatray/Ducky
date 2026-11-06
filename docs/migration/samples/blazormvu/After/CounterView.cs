using Ducky.Blazor;
using Microsoft.AspNetCore.Components.Rendering;

namespace BlazorMvu.After;

#region after-component
// Ducky 2.0: the component renders the slice and dispatches to the store; DevTools replaces the time-travel debugger.
public sealed class Counter : DuckyComponent<CounterState>
{
    public void Increment() => Dispatch(new Increment());

    public void Reload() => Dispatch(new LoadCount());

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, State.Count);
}
#endregion
