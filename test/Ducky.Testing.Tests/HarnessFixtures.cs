// Actions, slices and effects for SliceTestTests and EffectTestTests (SPEC §15).
namespace Ducky.Testing.Tests.Fixtures;

internal sealed record Tally(int Value);

internal sealed record Add(int By);

internal sealed record Keep;

internal sealed record Copy;

internal sealed record Fetch(string Name);

internal sealed record UnrelatedAction;

internal sealed record Fetched(string Name, int Value);

internal sealed record Prefix(string Value);

// Key "tally". Keep returns the same instance; Copy returns an equal new one; Fetch and UnrelatedAction are unhandled.
internal sealed class TallySlice : Slice<Tally>
{
    public TallySlice()
    {
        On<Add>((s, a) => s with { Value = s.Value + a.By });
        On<Keep>(s => s);
        On<Copy>(s => s with { });
    }

    protected override Tally Initial => new(0);
}

// Reduces StoreInitialized (+100) like a slice that marks itself loaded; Add is a broken no-op, Keep the same instance.
internal sealed class InitTallySlice : Slice<Tally>
{
    public InitTallySlice()
    {
        On<StoreInitialized>(s => s with { Value = s.Value + 100 });
        On<Add>(s => s);
        On<Keep>(s => s);
    }

    protected override Tally Initial => new(0);
}

// Init that never completes: every user action stays buffered, so Dispatch is not reduced inline (§6.7).
internal sealed class PendingInitMiddleware : Middleware
{
    public override async ValueTask InitializeAsync(CancellationToken cancellationToken) =>
        await new TaskCompletionSource().Task.WaitAsync(cancellationToken).ConfigureAwait(false);
}

// Dispatches Fetched(name, 1), yields, then Fetched(name, 2): no slice reduces Fetched.
internal class FetchEffect : Effect<Fetch>
{
    public override async Task Handle(Fetch action, EffectContext context, CancellationToken cancellationToken)
    {
        context.Dispatch(new Fetched(action.Name, 1));
        await Task.Yield();
        context.Dispatch(new Fetched(action.Name, 2));
    }
}

// A test double deriving from FetchEffect: dispatches Fetched("fake", 0) only.
internal sealed class FakeFetchEffect : FetchEffect
{
    public override Task Handle(Fetch action, EffectContext context, CancellationToken cancellationToken)
    {
        context.Dispatch(new Fetched("fake", 0));
        return Task.CompletedTask;
    }
}

// Reads the Tally slice through the effect context.
internal sealed class ReadTallyEffect : Effect<Fetch>
{
    public override Task Handle(Fetch action, EffectContext context, CancellationToken cancellationToken)
    {
        context.Dispatch(new Fetched(action.Name, context.State.Get<Tally>().Value));
        return Task.CompletedTask;
    }
}

internal sealed class CountingEffect : Effect<Fetch>
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public override Task Handle(Fetch action, EffectContext context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        context.Dispatch(new Fetched(action.Name, Calls));
        return Task.CompletedTask;
    }
}

// An EffectGroup injecting the store-owned CartStore and a service: reports the cart size, then adds the item.
internal sealed class CartEffects : EffectGroup
{
    public CartEffects(CartStore cart, Prefix prefix) =>
        On<Fetch>((action, context, _) =>
        {
            context.Dispatch(new Fetched(prefix.Value + action.Name, cart.State.Items.Count));
            cart.Add(action.Name);
            return Task.CompletedTask;
        });
}

// A fake service that counts the loads made through it.
internal sealed class LoadCalls
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public int Next() => Interlocked.Increment(ref _count);
}

// An initial-load effect (§12, §22.2): injects the service, calls it, dispatches the result.
internal sealed class InitLoadEffect(LoadCalls calls) : Effect<StoreInitialized>
{
    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        context.Dispatch(new Fetched("init", calls.Next()));
        return Task.CompletedTask;
    }
}
