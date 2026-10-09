using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Ducky.Blazor.Tests.SliceStore;

internal sealed record Cart(int Count);

// The §12 example's shape: a SliceStore whose methods mutate it through Set.
internal sealed class CartStore : SliceStore<Cart>
{
    public int Count => State.Count;

    public void Add() => Set(static s => s with { Count = s.Count + 1 });

    protected override Cart Initial => new(0);
}

// A plain component with an injected SliceStore: it reads the point-in-time State on each render and re-renders only
// after its own event handlers (Bustand's UseState mapping, §22.2).
internal sealed class PlainCartView : ComponentBase
{
    [Inject]
    public CartStore Cart { get; set; } = null!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, Cart.Add));
        builder.CloseElement();
        builder.OpenElement(2, "span");
        builder.AddContent(3, Cart.Count);
        builder.CloseElement();
    }
}

// The recommended shape: the markup reads through a selection, mutations still go through the SliceStore's methods.
internal sealed class SelectedCartView : DuckyComponent
{
    private Selection<int> _count = null!;

    [Inject]
    public CartStore Cart { get; set; } = null!;

    protected override void OnInitialized() => _count = Select((Cart s) => s.Count);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, Cart.Add));
        builder.CloseElement();
        builder.OpenElement(2, "span");
        builder.AddContent(3, _count.Value);
        builder.CloseElement();
    }
}

[JsonSerializable(typeof(Cart))]
internal sealed partial class SliceStoreJson : JsonSerializerContext;
