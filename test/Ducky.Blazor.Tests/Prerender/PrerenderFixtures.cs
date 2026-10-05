using System.Text.Json.Serialization;
using Ducky.Blazor.Tests.Core;

namespace Ducky.Blazor.Tests.Prerender;

internal sealed record ProductsLoaded(int Count);

internal sealed record ProductsFailed(string Error);

internal sealed record Products(int Count, string? Error);

// An app's own slice base: Prerender<TSlice>() finds the state type through any depth of base classes.
internal abstract class AppSlice<TState> : Slice<TState>
    where TState : class;

internal sealed class ProductsSlice : AppSlice<Products>
{
    public ProductsSlice()
    {
        On<ProductsLoaded>((state, action) => state with { Count = action.Count, Error = null });
        On<ProductsFailed>((state, action) => state with { Error = action.Error });
    }

    protected override Products Initial => new(0, null);
}

// A state the serializer refuses while it holds NaN (no AllowNamedFloatingPointLiterals).
internal sealed record Ratio(double Value);

internal sealed record SetRatio(double Value);

internal sealed class RatioSlice : Slice<Ratio>
{
    public RatioSlice() => On<SetRatio>((_, action) => new(action.Value));

    protected override Ratio Initial => new(1);
}

// A state as deep as DeepEnvelopeJson's MaxDepth of 128 allows.
internal sealed class DeepNestSlice : Slice<Persistence.Nest>
{
    protected override Persistence.Nest Initial => Persistence.Nest.Deep(127);
}

// How many times LoadProducts loaded, across every store of a test.
internal sealed class Loads
{
    public int Count { get; set; }
}

// The recommended load rule (§11.4): load at StoreInitialized unless the slice was restored.
internal sealed class LoadProducts(Loads loads) : Effect<StoreInitialized>
{
    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.State.WasRestored<Products>())
        {
            context.Dispatch(new ProductsLoaded(++loads.Count * 10));
        }

        return Task.CompletedTask;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Counter))]
[JsonSerializable(typeof(Products))]
[JsonSerializable(typeof(Ratio))]
internal sealed partial class PrerenderJson : JsonSerializerContext;
