using System.Collections;
using System.Text.Json.Serialization;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Microsoft.AspNetCore.Components;

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

// A text state, for seeds above the wire budget and quote-dense or CJK seeds.
internal sealed record Note(string Text);

internal sealed record SetNote(string Text);

internal sealed class NoteSlice : Slice<Note>
{
    public NoteSlice() => On<SetNote>((_, action) => new(action.Text));

    protected override Note Initial => new(string.Empty);
}

// How many times a load effect loaded, across every store of a test; whether LoadProducts fails (a prerender-time load
// calling a relative URL); and when SlowLoadCounter's load completes.
internal sealed class Loads
{
    public int Count { get; set; }

    public bool Fail { get; set; }

    public TaskCompletionSource Release { get; } = new();
}

// The recommended load rule (§11.4): load at StoreInitialized unless the slice was restored.
internal sealed class LoadProducts(Loads loads) : Effect<StoreInitialized>
{
    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.State.WasRestored<Products>())
        {
            context.Dispatch(loads.Fail ? new ProductsFailed("relative URL") : new ProductsLoaded(++loads.Count * 10));
        }

        return Task.CompletedTask;
    }
}

// The same rule for Counter, with a load that completes only when the test releases it: slower than the render.
internal sealed class SlowLoadCounter(Loads loads) : Effect<StoreInitialized>
{
    public override async Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.State.WasRestored<Counter>())
        {
            await loads.Release.Task.WaitAsync(cancellationToken);
            loads.Count++;
            context.Dispatch(new Increment());
        }
    }
}

// A poller: a LongRunning run that lasts as long as the store.
internal sealed class Poller : Effect<StoreInitialized>
{
    public override bool LongRunning => true;

    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken) =>
        new TaskCompletionSource().Task.WaitAsync(cancellationToken);
}

// An app configuring both render modes: the framework persists through one store per payload (server and WebAssembly)
// and calls every registration once per payload.
internal sealed class CompositeStateStore : IPersistentComponentStateStore, IEnumerable<IPersistentComponentStateStore>
{
    public FakeComponentStateStore Server { get; } = new();

    public FakeComponentStateStore Wasm { get; } = new();

    public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => throw new NotSupportedException();

    public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state) => throw new NotSupportedException();

    public IEnumerator<IPersistentComponentStateStore> GetEnumerator() => ((IEnumerable<IPersistentComponentStateStore>)[Server, Wasm]).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Counter))]
[JsonSerializable(typeof(Products))]
[JsonSerializable(typeof(Ratio))]
[JsonSerializable(typeof(Note))]
internal sealed partial class PrerenderJson : JsonSerializerContext;
