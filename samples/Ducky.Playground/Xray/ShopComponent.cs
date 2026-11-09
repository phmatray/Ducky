// Every shop component counts its renders and X-ray shows the count. A DuckyComponent
// re-renders only when a value it selected changed, so most counters stay put.
using Ducky.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Ducky.Playground.Xray;

public abstract class ShopComponent : DuckyComponent
{
    private int _renderCount;

    // Rendered inline by the component's own markup (@RenderCount): it counts this
    // component's renders and causes none.
    protected RenderFragment RenderCount => CountRender;

    private void CountRender(RenderTreeBuilder builder)
    {
        _renderCount++;
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "renders");
        builder.AddContent(2, _renderCount == 1 ? "1 render" : $"{_renderCount} renders");
        builder.CloseElement();
    }
}
