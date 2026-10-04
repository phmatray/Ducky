using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.MaterializationFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests;

// SPEC §6.6 "Materialization and disposal" and §6.11 "Server disposal order": INV-29, INV-31.
public sealed class MaterializationTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    private static ServiceProvider Provider(Ledger ledger, Action<DuckyBuilder> configure, TimeProvider? time = null) =>
        new ServiceCollection()
            .AddSingleton(ledger)
            .AddSingleton(time ?? TimeProvider.System)
            .AddLogging(logging => logging.AddFakeLogging().SetMinimumLevel(LogLevel.Debug))
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Scoped;
                d.AddSlice<CountSlice>();
                configure(d);
            })
            .BuildServiceProvider();

    // A server DuckyComponent that injected IStore but never used it: disposing its scope runs no user constructor and
    // resolves nothing from the disposing scope (§6.6, §6.11), and has no materialization to wait for (the fake clock
    // never reaches DisposeTimeout).
    [Fact]
    public async Task Dispose_NeverMaterialized_ConstructsNothing()
    {
        var ledger = new Ledger();
        await using var provider = Provider(
            ledger,
            d =>
            {
                d.AddEffect<OwnedEffect>();
                d.Use<LedgerMiddleware>();
            },
            new FakeTimeProvider());
        var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();

        await scope.DisposeAsync().AsTask().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);

        ledger.Constructed.ShouldBeEmpty();
        ledger.Disposed.ShouldBeEmpty();
        _ = store.State;
        ledger.Constructed.ShouldBeEmpty();
        provider.GetFakeLogCollector().GetSnapshot().ShouldBeEmpty();
    }

    // Disposal closes materialization: a store whose constructor would throw DUCKY353 is never materialized after it, and
    // every entry point takes its after-disposal path instead (§6.6, §6.11).
    [Fact]
    public async Task Dispatch_AfterDisposeOnUnmaterializedStore_IgnoredNotThrown()
    {
        var ledger = new Ledger();
        await using var provider = Provider(ledger, d => d.Use<ThrowingCtorMiddleware>());
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        await store.DisposeAsync();
        var ct = TestContext.Current.CancellationToken;

        store.Dispatch(new Bump());

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Disposed);
        store.Restore(new Dictionary<string, object> { ["count"] = new Count(7) }, Origin.Hydration);
        store.State.Get<Count>().Value.ShouldBe(0);
        store.Select(s => s.Get<Count>().Value, _ => { }).Value.ShouldBe(0);
        await store.InitializeAsync(ct);
        await store.WhenIdleAsync(ct);
        ledger.Constructed.ShouldBeEmpty();
        var records = provider.GetFakeLogCollector().GetSnapshot();
        records.Count.ShouldBe(3);
        records.ShouldAllBe(r => r.Id.Id == 1002 && r.Level == LogLevel.Debug);
        records[0].Message.ShouldBe($"{typeof(Bump)} ignored: the store is disposed");
    }

    // The deterministic twin of the §17.3 race: a middleware constructor disposes the store while materialization is in
    // flight. That disposal gets an incomplete task (it awaits the factory), and once the factory ends it disposes every
    // middleware the factory built, the ones constructed after the call included; the dispatch that materialized takes
    // its after-disposal path.
    [Fact]
    public async Task Dispose_ConcurrentWithFirstUse_EveryConstructedMiddlewareDisposed_Deterministic()
    {
        var ledger = new Ledger();
        DuckyStore store = null!;
        Task? disposal = null;
        bool? completedInCtor = null;
        store = new DuckyStore(
            [new CountSlice()],
            NullLogger.Instance,
            disposeTimeout: Timeout.InfiniteTimeSpan,
            middleware: () =>
            [
                new LedgerMiddleware(ledger),
                new CtorAction(() =>
                {
                    disposal = store.DisposeAsync().AsTask();
                    completedInCtor = disposal.IsCompleted;
                }),
                new FaultyDisposeMiddleware(ledger),
            ]);

        store.Dispatch(new Bump());

        completedInCtor.ShouldBe(false);
        store.State.Get<Count>().Value.ShouldBe(0);
        await disposal.ShouldNotBeNull().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        ledger.Constructed.ShouldBe([nameof(LedgerMiddleware), nameof(FaultyDisposeMiddleware)]);
        ledger.Disposed.ShouldBe([nameof(FaultyDisposeMiddleware), nameof(LedgerMiddleware)]);
    }

    // Non-normative: a materialization that outlasts DisposeTimeout. DisposeAsync completes at that bound, while 5a stays
    // chained on the factory's end: the middleware constructed after the bound is still disposed, never early (§6.11).
    [Fact]
    public async Task Dispose_MaterializationOutlastsDisposeTimeout_CompletesAtBoundAndDisposesBuiltLater()
    {
        var time = new FakeTimeProvider();
        var ledger = new Ledger();
        DuckyStore store = null!;
        Task? disposal = null;
        var beforeBound = true;
        var atBound = false;
        store = new DuckyStore(
            [new CountSlice()],
            NullLogger.Instance,
            disposeTimeout: TimeSpan.FromSeconds(5),
            timeProvider: time,
            middleware: () =>
            [
                new CtorAction(() =>
                {
                    disposal = store.DisposeAsync().AsTask();
                    time.Advance(TimeSpan.FromSeconds(4));
                    beforeBound = disposal.IsCompleted;
                    time.Advance(TimeSpan.FromSeconds(1));
                    atBound = disposal.IsCompleted;
                }),
                new LedgerMiddleware(ledger),
            ]);

        store.Dispatch(new Bump());

        beforeBound.ShouldBeFalse();
        atBound.ShouldBeTrue();
        await store.Dispatcher.DisposalSteps.ShouldNotBeNull().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        ledger.Disposed.ShouldBe([nameof(LedgerMiddleware)]);
    }

    // Non-normative: the store's disposal begins inside materialization (a constructor disposing it through a test-held
    // reference) while its scope is alive, so the factory never resolves the dispose hook, and the disposal still disposes
    // what the factory built (§6.6).
    [Fact]
    public async Task Materialization_DisposalBeganDuringFactory_DisposeHookSkippedAndBuiltDisposed()
    {
        var ledger = new Ledger();
        var hooks = 0;
        IStore store = null!;
        Task? disposal = null;
        var services = new ServiceCollection()
            .AddSingleton(ledger)
            .AddSingleton<Action>(() => disposal = store.DisposeAsync().AsTask())
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Scoped;
                d.AddSlice<CountSlice>();
                d.Use<LedgerMiddleware>();
                d.Use<CtorAction>();
            })
            .Replace(ServiceDescriptor.Scoped(sp =>
            {
                hooks++;
                return new StoreDisposeHook(sp.GetRequiredService<IStore>());
            }));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        store = scope.ServiceProvider.GetRequiredService<IStore>();

        store.Dispatch(new Bump());

        await disposal.ShouldNotBeNull().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        hooks.ShouldBe(0);
        store.State.Get<Count>().Value.ShouldBe(0);
        ledger.Disposed.ShouldBe([nameof(LedgerMiddleware)]);
    }

    // Non-normative: a scoped service created after a Scoped store (no effects, no middleware) reads it from its own
    // DisposeAsync, the store's first use, while the scope disposes. Its dispose hook can't be resolved from that scope any
    // more and has nothing left to do (the scope disposes the store next), so materialization skips it instead of caching
    // ObjectDisposedException (§6.11).
    [Fact]
    public async Task Materialization_FirstUseWhileScopeDisposing_DisposeHookSkippedNotThrown()
    {
        await using var provider = new ServiceCollection()
            .AddScoped<LateService>()
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Scoped;
                d.AddSlice<CountSlice>();
            })
            .BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var late = scope.ServiceProvider.GetRequiredService<LateService>();

        await scope.DisposeAsync();

        late.Count.ShouldBe(0);
    }

    // A constructor that throws at materialization: the store-owned instances built before it (in construction order:
    // effects, then middleware) are disposed in reverse order, each in its own try/catch (Error 1015), before DUCKY353 is
    // cached; the AddEffect(instance) instance is never disposed, and the store's own disposal disposes nothing again and
    // never rethrows DUCKY353 (§6.6, INV-31).
    [Fact]
    public async Task Materialization_CtorThrows_EarlierInstancesDisposed()
    {
        var ledger = new Ledger();
        var instance = new InstanceEffect();
        await using var provider = Provider(ledger, d =>
        {
            d.AddEffect<OwnedEffect>();
            d.AddEffect(instance);
            d.Use<FaultyDisposeMiddleware>();
            d.Use<LedgerMiddleware>();
            d.Use<ThrowingCtorMiddleware>();
        });
        var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();

        var thrown = Should.Throw<DuckyConfigurationException>(() => store.Dispatch(new Bump()));

        thrown.Errors.ShouldHaveSingleItem().Code.ShouldBe("DUCKY353");
        ledger.Constructed.ShouldBe([nameof(OwnedEffect), nameof(FaultyDisposeMiddleware), nameof(LedgerMiddleware), nameof(ThrowingCtorMiddleware)]);
        ledger.Disposed.ShouldBe([nameof(LedgerMiddleware), nameof(FaultyDisposeMiddleware), nameof(OwnedEffect)]);
        instance.Disposed.ShouldBeFalse();
        var record = provider.GetFakeLogCollector().GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1015);
        record.Level.ShouldBe(LogLevel.Error);
        record.Message.ShouldBe($"Disposing {typeof(FaultyDisposeMiddleware)} threw");
        Should.Throw<DuckyConfigurationException>(() => _ = store.State).ShouldBeSameAs(thrown);

        await scope.DisposeAsync();

        ledger.Disposed.Count.ShouldBe(3);
        instance.Disposed.ShouldBeFalse();
        store.Dispatch(new Bump());
    }

    // Non-normative: a constructor that throws once disposal has begun (the next one resolves from the scope the
    // constructor before it started disposing, the §6.6 case) is not rethrown: the materializing caller takes its after-disposal path, the instances the factory built
    // are disposed, and DisposeAsync never rethrows DUCKY353 (§6.6).
    [Fact]
    public async Task Materialization_CtorThrowsAfterDisposalBegan_AfterDisposalPathNotRethrown()
    {
        var ledger = new Ledger();
        var holder = new ScopeHolder();
        var services = new ServiceCollection()
            .AddSingleton(ledger)
            .AddSingleton(holder)
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Scoped;
                d.AddSlice<CountSlice>();
                d.Use<LedgerMiddleware>();
                d.Use<ScopeDisposingMiddleware>();
                d.Use<ThrowingCtorMiddleware>();
            });
        await using var provider = services.BuildServiceProvider();
        holder.Scope = provider.CreateAsyncScope();
        var store = holder.Scope.Value.ServiceProvider.GetRequiredService<IStore>();

        store.Dispatch(new Bump());

        await holder.Disposal.ShouldNotBeNull().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        await store.DisposeAsync();
        ledger.Constructed.ShouldBe([nameof(LedgerMiddleware)]);
        ledger.Disposed.ShouldBe([nameof(LedgerMiddleware)]);
        _ = store.State;
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Disposed);
    }

    // The scoped StoreDisposeHook, resolved last at materialization, is disposed first by the server scope: the store is
    // disposed (its effects included) while the scoped dependencies its effects resolved are still alive (§6.11).
    [Fact]
    public async Task ServerScopeDispose_EffectScopedDependencyDisposedAfterStoreDispose()
    {
        var journal = new EffectFixtures.EffectJournal();
        var services = new ServiceCollection()
            .AddSingleton(journal)
            .AddScoped<EffectFixtures.ScopedProbe>()
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Scoped;
                d.AddSlice<EffectFixtures.SeenSlice>();
                d.AddEffect<EffectFixtures.ScopedEffect>();
                d.Use<EffectFixtures.JournalMiddleware>();
            });
        await using var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        (await store.DispatchAsync(new EffectFixtures.Load(1))).ShouldBe(DispatchResult.Reduced);

        await scope.DisposeAsync();

        journal.Disposed.ShouldBe([nameof(EffectFixtures.JournalMiddleware), nameof(EffectFixtures.ScopedEffect), nameof(EffectFixtures.ScopedProbe)]);
    }

    // Non-normative: a container that disposes synchronously reaches the hook's Dispose, which is the store's Dispose
    // (Warning 1010, §6.11): the hook, created after the middleware's scoped dependency, is disposed before it, so the
    // store's disposal has begun when the scope disposes that dependency.
    [Fact]
    public void ServerScopeSyncDispose_HookStartsStoreDisposalBeforeScopedDependency()
    {
        using var provider = new ServiceCollection()
            .AddScoped<Witness>()
            .AddLogging(logging => logging.AddFakeLogging())
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Scoped;
                d.AddSlice<CountSlice>();
                d.Use<WitnessMiddleware>();
            })
            .BuildServiceProvider();
        var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        store.Dispatch(new Bump());
        var witness = scope.ServiceProvider.GetRequiredService<Witness>();

        scope.Dispose();

        witness.StoreDisposalBegan.ShouldBe(true);
        provider.GetFakeLogCollector().GetSnapshot().ShouldContain(r => r.Id.Id == 1010);
    }

    // A constructor throws while the last middleware built is still disposing asynchronously. Every disposal is started,
    // in reverse construction order, before DUCKY353 is cached (blocking the factory on the async rest is forbidden,
    // INV-05); the store's DisposeAsync awaits that cleanup (the materialization wait) before the store scope (§6.6, §6.11).
    [Fact]
    public async Task Materialization_CtorThrowsWhileDisposalPending_EveryDisposalStartedThenAwaited()
    {
        var ledger = new Ledger();
        var gate = new DisposeGate();
        await using var provider = SingletonProvider(ledger, gate, TimeProvider.System);
        var store = provider.GetRequiredService<IStore>();

        Should.Throw<DuckyConfigurationException>(() => store.Dispatch(new Bump()));

        ledger.Disposed.ShouldBe([nameof(LedgerMiddleware), nameof(OwnedEffect)]);
        var disposal = store.DisposeAsync().AsTask();
        disposal.IsCompleted.ShouldBeFalse();
        gate.Open.SetResult();
        await disposal.WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        ledger.Disposed.ShouldBe([nameof(LedgerMiddleware), nameof(OwnedEffect), nameof(SlowDisposeMiddleware), nameof(StoreScopedDependency)]);
    }

    // A middleware DisposeAsync that hangs during that cleanup is bounded like phase 5a (Warning 1016): the instances built
    // before it are still disposed, and so is the store scope once the bound expires (INV-29, §6.6).
    [Fact]
    public async Task Materialization_CtorThrowsWhileDisposalHangs_BoundedAndRestDisposed()
    {
        var time = new FakeTimeProvider();
        var ledger = new Ledger();
        await using var provider = SingletonProvider(ledger, new DisposeGate(), time);
        var store = provider.GetRequiredService<IStore>();
        Should.Throw<DuckyConfigurationException>(() => store.Dispatch(new Bump()));

        var disposal = store.DisposeAsync().AsTask();
        time.Advance(TimeSpan.FromSeconds(2));

        await disposal.WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        await ((DuckyStore)store).Dispatcher.DisposalSteps.ShouldNotBeNull().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        ledger.Disposed.ShouldBe([nameof(LedgerMiddleware), nameof(OwnedEffect), nameof(StoreScopedDependency)]);
        var record = provider.GetFakeLogCollector().GetSnapshot().Where(r => r.Id.Id == 1016).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
    }

    // A Singleton store whose scope owns SlowDisposeMiddleware's transient dependency, built after an owned effect and a
    // middleware, and before a throwing constructor.
    private static ServiceProvider SingletonProvider(Ledger ledger, DisposeGate gate, TimeProvider time) =>
        new ServiceCollection()
            .AddSingleton(ledger)
            .AddSingleton(gate)
            .AddSingleton(time)
            .AddTransient<StoreScopedDependency>()
            .AddLogging(logging => logging.AddFakeLogging().SetMinimumLevel(LogLevel.Debug))
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Singleton;
                d.AddSlice<CountSlice>();
                d.AddEffect<OwnedEffect>();
                d.Use<LedgerMiddleware>();
                d.Use<SlowDisposeMiddleware>();
                d.Use<ThrowingCtorMiddleware>();
            })
            .BuildServiceProvider();
}
