// Effects and middleware for MaterializationTests (SPEC §6.6 materialization and disposal, §6.11 server disposal order).
using Ducky.Tests.DispatcherFixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.MaterializationFixtures;

// Shared by the store-created instances below, resolved from DI: what was constructed and what was disposed, in order.
internal sealed class Ledger
{
    public List<string> Constructed { get; } = [];

    public List<string> Disposed { get; } = [];
}

internal sealed class OwnedEffect : Effect<Bump>, IDisposable
{
    private readonly Ledger _ledger;

    public OwnedEffect(Ledger ledger)
    {
        _ledger = ledger;
        ledger.Constructed.Add(nameof(OwnedEffect));
    }

    public override Task Handle(Bump action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _ledger.Disposed.Add(nameof(OwnedEffect));
}

// An AddEffect(instance) instance: used, never disposed (§5.1).
internal sealed class InstanceEffect : Effect<Ping>, IDisposable
{
    public bool Disposed { get; private set; }

    public override Task Handle(Ping action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => Disposed = true;
}

// Its DisposeAsync faults: Error 1015, and the instances built before it are still disposed.
internal sealed class FaultyDisposeMiddleware : Middleware
{
    private readonly Ledger _ledger;

    public FaultyDisposeMiddleware(Ledger ledger)
    {
        _ledger = ledger;
        ledger.Constructed.Add(nameof(FaultyDisposeMiddleware));
    }

    public override async ValueTask DisposeAsync()
    {
        _ledger.Disposed.Add(nameof(FaultyDisposeMiddleware));
        await base.DisposeAsync();
        throw new InvalidOperationException("dispose");
    }
}

internal sealed class LedgerMiddleware : Middleware
{
    private readonly Ledger _ledger;

    public LedgerMiddleware(Ledger ledger)
    {
        _ledger = ledger;
        ledger.Constructed.Add(nameof(LedgerMiddleware));
    }

    public override ValueTask DisposeAsync()
    {
        _ledger.Disposed.Add(nameof(LedgerMiddleware));
        return base.DisposeAsync();
    }
}

internal sealed class ThrowingCtorMiddleware : Middleware
{
    public ThrowingCtorMiddleware(Ledger ledger)
    {
        ledger.Constructed.Add(nameof(ThrowingCtorMiddleware));
        throw new FormatException("ctor");
    }
}

// A middleware whose constructor runs the test's action: a disposal started from inside materialization (§6.6).
internal sealed class CtorAction : Middleware
{
    public CtorAction(Action action) => action();
}

// Its constructor disposes the scope the store resolves from, through a test-held reference: the container's disposal of
// the store begins while materialization is in flight, before the dispose hook would be resolved (§6.6, §6.10).
internal sealed class ScopeHolder
{
    public AsyncServiceScope? Scope { get; set; }

    public Task? Disposal { get; set; }
}

internal sealed class ScopeDisposingMiddleware : Middleware
{
    public ScopeDisposingMiddleware(ScopeHolder holder) => holder.Disposal = holder.Scope!.Value.DisposeAsync().AsTask();
}

// A scoped service created after the store, whose disposal is the store's first use: the store materializes while its
// scope is disposing, before the container reaches the store itself (§6.11).
internal sealed class LateService(IStore store) : IAsyncDisposable
{
    public int Count { get; private set; } = -1;

    public ValueTask DisposeAsync()
    {
        Count = store.State.Get<Count>().Value;
        return default;
    }
}

// A scoped dependency of a middleware: whether the store's disposal had begun when the scope disposed it (§6.11).
internal sealed class Witness(IStore store) : IDisposable
{
    public bool? StoreDisposalBegan { get; private set; }

    public void Dispose() => StoreDisposalBegan = ((DuckyStore)store).Dispatcher.DisposalBegan;
}

internal sealed class WitnessMiddleware(Witness witness) : Middleware
{
    public Witness Witness { get; } = witness;
}

// Its DisposeAsync completes only once the test opens the gate: a disposal that is still running when the factory throws.
internal sealed class DisposeGate
{
    public TaskCompletionSource Open { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

// Resolved from the store's own scope: disposed by phase 5b, with that scope.
internal sealed class StoreScopedDependency(Ledger ledger) : IDisposable
{
    public void Dispose() => ledger.Disposed.Add(nameof(StoreScopedDependency));
}

internal sealed class SlowDisposeMiddleware(Ledger ledger, StoreScopedDependency dependency, DisposeGate gate) : Middleware
{
    public StoreScopedDependency Dependency { get; } = dependency;

    public override async ValueTask DisposeAsync()
    {
        await gate.Open.Task;
        ledger.Disposed.Add(nameof(SlowDisposeMiddleware));
        await base.DisposeAsync();
    }
}
