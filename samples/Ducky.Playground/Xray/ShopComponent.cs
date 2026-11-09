using Ducky.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Ducky.Playground.Xray;

// The render counter X-ray shows on each component. A DuckyComponent re-renders only when a value it selected changed
// (compared on the renderer with the value it last rendered), so these numbers stay put while the rest of the shop moves.
public abstract class ShopComponent : DuckyComponent
{
    private int _renders;

    // Rendered inline by the component's own markup (@RenderCount): it counts this component's renders and causes none.
    protected RenderFragment RenderCount => builder => Count(builder);

    private void Count(RenderTreeBuilder builder)
    {
        _renders++;
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "renders");
        builder.AddContent(2, _renders == 1 ? "1 render" : $"{_renders} renders");
        builder.CloseElement();
    }
}
