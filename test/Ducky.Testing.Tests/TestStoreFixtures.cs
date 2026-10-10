// Actions, slices and effects for TestStoreTests (SPEC §15).
using System.Collections.Immutable;

namespace Ducky.Testing.Tests.Fixtures;

internal sealed record Counter(int Value);

internal sealed record Increment;

internal sealed record Boom;

internal sealed record Fail;

internal sealed record Load;

internal sealed record Loaded;

internal sealed record Unhandled;

internal sealed record QuotaExceeded(string Reason);

internal sealed record NotRegistered;

// Key "counter".
internal sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice()
    {
        On<Increment>(s => s with { Value = s.Value + 1 });
        On<Loaded>(s => s with { Value = 100 });
        On<Boom>(_ => throw new InvalidOperationException("boom"));
    }

    protected override Counter Initial => new(0);
}

// A base state record and a record derived from it, each declared by its own slice (§15: keys by declared type).
internal record BaseState(string Name);

internal sealed record DerivedState(string Name, int Extra) : BaseState(Name);

internal sealed class BaseSlice : Slice<BaseState>
{
    protected override BaseState Initial => new("base");
}

internal sealed class DerivedSlice : Slice<DerivedState>
{
    protected override DerivedState Initial => new("derived", 0);
}

internal sealed record Cart(ImmutableList<string> Items);

internal sealed class CartStore : SliceStore<Cart>
{
    protected override Cart Initial => new([]);

    public void Add(string item) => Set(s => s with { Items = s.Items.Add(item) });
}

// Waits an hour on the store's TimeProvider, then dispatches Loaded.
internal sealed class LoadEffect : Effect<Load>
{
    public override async Task Handle(Load action, EffectContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromHours(1), context.Time, cancellationToken).ConfigureAwait(false);
        context.Dispatch(new Loaded());
    }
}

internal sealed class FailEffect : Effect<Fail>
{
    public override Task Handle(Fail action, EffectContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("effect failed");
}

// Dispatches the custom failure action, which no slice reduces.
internal sealed class QuotaEffect : Effect<Increment>
{
    public override Task Handle(Increment action, EffectContext context, CancellationToken cancellationToken)
    {
        context.Dispatch(new QuotaExceeded("full"));
        return Task.CompletedTask;
    }
}

// Holds init open until the test completes the gate.
internal sealed class GatedInitMiddleware(TaskCompletionSource gate) : Middleware
{
    public override async ValueTask InitializeAsync(CancellationToken cancellationToken) =>
        await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
}

internal sealed record Hang;

// Ignores its token: it waits on the gate the test completes, so the store's dispose bounds (on the FakeTimeProvider
// nobody advances) never end the run.
internal sealed class HangEffect(TaskCompletionSource gate) : Effect<Hang>
{
    public override Task Handle(Hang action, EffectContext context, CancellationToken cancellationToken) => gate.Task;
}

// Ignores its token: its init ends only when the test completes the gate, so StoreInitialized is never reduced.
internal sealed class HangInitMiddleware(TaskCompletionSource gate) : Middleware
{
    public override ValueTask InitializeAsync(CancellationToken cancellationToken) => new(gate.Task);
}
