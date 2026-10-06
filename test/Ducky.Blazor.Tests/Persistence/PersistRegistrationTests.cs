using System.Text.Json.Nodes;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Prerender;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor.Tests.Persistence;

// SPEC §11.1 (Persist<T>, PersistAttributeDefaults<T>, the configuration checks), §11.5 (the persistence slice), §8.2
// (DUCKY310-312, DUCKY316), ADR-0012; INV-14, INV-31.
public sealed class PersistRegistrationTests
{
    private static readonly ServiceProviderOptions _validating = new() { ValidateScopes = true, ValidateOnBuild = true };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ClearPersistedState_Strict_NoFailure()
    {
        // The persistence slice handles the public clear with a no-op, so strict mode never reports it as unhandled, and
        // the clear changes no in-memory state (storage only).
        await using var provider = Build(static d =>
        {
            d.ThrowOnUnhandledAction = true;
            d.AddBlazor().AddSlice<CounterSlice>().AddSlice<FailureSlice>();
        });
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        await store.InitializeAsync(Ct);
        (await store.DispatchAsync(new Increment())).ShouldBe(DispatchResult.Reduced);
        var before = store.State;

        (await store.DispatchAsync(new ClearPersistedState())).ShouldBe(DispatchResult.Reduced);
        await store.WhenIdleAsync(Ct);

        store.State.Get<Failures>().Items.ShouldBeEmpty();
        store.State.Get<PersistenceState>().ShouldBeSameAs(before.Get<PersistenceState>());
        store.State.Get<Counter>().ShouldBeSameAs(before.Get<Counter>());
        new ClearPersistedState().ShouldBe(new ClearPersistedState());
    }

    [Fact]
    public async Task Hydration_StaleEpochTerminal_IgnoredBySlice()
    {
        // A terminal carrying another epoch belongs to a superseded attempt: the slice keeps its status (defence in
        // depth). A terminal of the current epoch moves the status and keeps the epoch.
        await using var provider = Build(static d => d.AddBlazor());
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        await store.InitializeAsync(Ct);
        var key = scope.ServiceProvider.GetRequiredService<PersistenceSlice>().Key;
        key.ShouldBe("@ducky/persistence");
        store.State.Get<PersistenceState>().ShouldBe(new PersistenceState(PersistenceStatus.Hydrated));

        Hydrating(store, key, epoch: 2);
        (await store.DispatchAsync(new HydrationCompleted(StateRestored: true) { ScopeEpoch = 1 })).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new HydrationFailed("TimeoutException", "Timed out.") { ScopeEpoch = 3 })).ShouldBe(DispatchResult.Reduced);
        store.State.Get<PersistenceState>().ShouldBe(new PersistenceState(PersistenceStatus.Hydrating) { ScopeEpoch = 2 });

        await store.DispatchAsync(new HydrationCompleted(StateRestored: false) { ScopeEpoch = 2 });
        store.State.Get<PersistenceState>().ShouldBe(new PersistenceState(PersistenceStatus.Hydrated) { ScopeEpoch = 2 });

        Hydrating(store, key, epoch: 4);
        await store.DispatchAsync(new HydrationFailed("TimeoutException", "Timed out.") { ScopeEpoch = 4 });
        store.State.Get<PersistenceState>().ShouldBe(new PersistenceState(PersistenceStatus.Failed) { ScopeEpoch = 4 });
        (await store.DispatchAsync(new HydrationCompleted(StateRestored: true) { ScopeEpoch = 2 })).ShouldBe(DispatchResult.Reduced);
        store.State.Get<PersistenceState>().ShouldBe(new PersistenceState(PersistenceStatus.Failed) { ScopeEpoch = 4 });

        // The terminals are value records; the epoch is part of their identity.
        var completed = new HydrationCompleted(true) { ScopeEpoch = 4 };
        completed.StateRestored.ShouldBeTrue();
        completed.ShouldNotBe(new HydrationCompleted(true));
        var failed = new HydrationFailed("IOException", "Gone.");
        (failed.ErrorType, failed.Message, failed.ScopeEpoch).ShouldBe(("IOException", "Gone.", 0));

        static void Hydrating(IStore store, string key, int epoch) => store.Restore(
            new Dictionary<string, object> { [key] = new PersistenceState(PersistenceStatus.Hydrating) { ScopeEpoch = epoch } }, Origin.Hydration);
    }

    [Fact]
    public async Task Persist_ConfigureComposesInCallOrder()
    {
        // Every Persist<T> delegate runs once, in call order, on one PersistOptions per slice, after the attribute's base
        // layer whatever the call order: the last value written wins, and the builder wins over the attribute.
        List<string> seen = [];
        Func<JsonNode, JsonNode> first = static node => node;
        Func<JsonNode, JsonNode> last = static node => node;
        Func<JsonNode, JsonNode> two = static node => node;
        await using var provider = Build(d => d
            .AddSlice<CounterSlice>()
            .AddSlice<TallySlice>()
            .Persist<CounterSlice>(o =>
            {
                seen.Add($"first:{o.Storage}:{o.Version}");
                o.Version = 3;
                o.MaxAge = TimeSpan.FromDays(1);
                o.Migrate(1, first).Migrate(2, two).ShouldBeSameAs(o);
            })
            .PersistAttributeDefaults<CounterSlice>(o =>
            {
                seen.Add($"attribute:{o.Storage}:{o.Version}:{o.SyncAcrossTabs}");
                o.Version = 2;
                o.Storage = PersistStorage.Session;
            })
            .Persist<CounterSlice>()
            .Persist<CounterSlice>(o =>
            {
                seen.Add($"third:{o.Storage}:{o.Version}:{o.MaxAge}");
                o.Migrate(1, last);
                o.Debounce = TimeSpan.FromMilliseconds(40);
            })
            .Persist<TallySlice>());

        // Composed at first resolution, never when a builder call runs, and once however often the store is resolved.
        seen.ShouldBeEmpty();
        await using (var scope = provider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IStore>().Slices.OfType<PersistenceSlice>().ShouldHaveSingleItem();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IStore>().ShouldNotBeNull();
        }

        seen.ShouldBe(["attribute:Local:1:False", "first:Session:2", "third:Session:3:1.00:00:00"]);
        var persist = provider.GetRequiredService<BlazorRegistration>().Persist;
        persist.Keys.ShouldBe([typeof(CounterSlice), typeof(TallySlice)]);
        var counter = persist[typeof(CounterSlice)].Options;
        (counter.Storage, counter.Version, counter.MaxAge, counter.Debounce, counter.SyncAcrossTabs)
            .ShouldBe((PersistStorage.Session, 3, TimeSpan.FromDays(1), TimeSpan.FromMilliseconds(40), false));
        counter.Migrations.Keys.ShouldBe([1, 2], ignoreOrder: true);
        counter.Migrations[1].ShouldBeSameAs(last);
        counter.Migrations[2].ShouldBeSameAs(two);

        // A slice without a delegate keeps every default.
        var tally = persist[typeof(TallySlice)].Options;
        (tally.Storage, tally.Version, tally.MaxAge, tally.Debounce, tally.SyncAcrossTabs).ShouldBe((PersistStorage.Local, 1, null, null, false));
        tally.Migrations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Persist_RegistersBlazorAndRequiresJsonTypeInfo()
    {
        // Persist<T> and PersistAttributeDefaults<T> each add Ducky.Blazor and require the state's JSON type info (DUCKY306).
        await using (var provider = Build(static d => d.AddSlice<CounterSlice>().Persist<CounterSlice>()))
        {
            await using var scope = provider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IStore>().Slices.Select(static slice => slice.GetType())
                .ShouldBe([typeof(CounterSlice), typeof(PersistenceSlice)]);
        }

        await using (var provider = Build(static d => d.AddSlice<CounterSlice>().PersistAttributeDefaults<CounterSlice>(static _ => { })))
        {
            await using var scope = provider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IStore>().Slices.OfType<PersistenceSlice>().ShouldHaveSingleItem();
        }

        var exception = ResolveFails(static d => d
            .UseJson(PrerenderJson.Default)
            .AddSlice<CounterSlice>()
            .AddSlice<TallySlice>()
            .AddSlice<StatusSlice>()
            .Persist<CounterSlice>()
            .Persist<TallySlice>()
            .PersistAttributeDefaults<TallySlice>(static _ => { })
            .PersistAttributeDefaults<StatusSlice>(static _ => { }));

        exception.Errors.Select(static error => error.Code).ShouldBe(["DUCKY306", "DUCKY306"]);
        exception.Errors[0].Message.ShouldContain(typeof(Tally).FullName!);
        exception.Errors[0].Message.ShouldContain("Persist<TallySlice>");
        exception.Errors[1].Message.ShouldContain(typeof(Status).FullName!);
        exception.Errors[1].Message.ShouldContain("Persist<StatusSlice>");
    }

    [Fact]
    public void Persist_NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => DuckyBlazorBuilderExtensions.Persist<CounterSlice>(null!)).ParamName.ShouldBe("builder");
        Should.Throw<ArgumentNullException>(() => DuckyBlazorBuilderExtensions.PersistAttributeDefaults<CounterSlice>(null!, static _ => { }))
            .ParamName.ShouldBe("builder");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddDucky(static d => d.PersistAttributeDefaults<CounterSlice>(null!)))
            .ParamName.ShouldBe("configure");
        Should.Throw<ArgumentNullException>(() => new PersistOptions().Migrate(1, null!)).ParamName.ShouldBe("step");
    }

    [Fact]
    public void Validation_Ducky310_311_312_316_Reported()
    {
        // Checked together at first resolution, on the final composed options.
        var exception = ResolveFails(static d =>
        {
            d.AddSlice<CounterSlice>().AddSlice<TallySlice>().AddSlice<StatusSlice>().AddSlice<ProductsSlice>().AddSlice<RatioSlice>();
            d.Persist<OrphanSlice>();                                                 // DUCKY310
            d.Persist<CounterSlice>(static o => o.Version = 3);                       // DUCKY311: no step from 1 nor 2
            d.Persist<TallySlice>(static o => o.Migrate(1, Same).Version = 4);        // DUCKY311: a hole at 2 and 3
            d.Persist<StatusSlice>(static o =>                                        // DUCKY312
            {
                o.Storage = PersistStorage.Session;
                o.SyncAcrossTabs = true;
            });
            d.Persist<ProductsSlice>(static o =>                                      // DUCKY312
            {
                o.Storage = PersistStorage.Server;
                o.SyncAcrossTabs = true;
            });
            d.Prerender<OrphanSlice>().Prerender<RatioSlice>();                       // DUCKY310 for the orphan
            d.AddBlazor(static o =>                                                   // DUCKY316 twice: equal is not shorter
            {
                o.HydrationTimeout = TimeSpan.FromSeconds(4);
                o.PrerenderSeedWaitTimeout = TimeSpan.FromSeconds(4);
            });
            d.InitTimeout = TimeSpan.FromSeconds(4);
        });

        exception.Errors.ShouldBe([
            BlazorErrors.SliceNotAdded("Prerender", typeof(OrphanSlice)),
            BlazorErrors.HydrationTimeoutNotBelowInitTimeout(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)),
            BlazorErrors.SeedWaitNotBelowHydrationTimeout(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)),
            BlazorErrors.SliceNotAdded("Persist", typeof(OrphanSlice)),
            BlazorErrors.MigrationGap(typeof(CounterSlice), 3, [1, 2]),
            BlazorErrors.MigrationGap(typeof(TallySlice), 4, [2, 3]),
            BlazorErrors.SyncAcrossTabsWithoutLocal(typeof(StatusSlice), PersistStorage.Session),
            BlazorErrors.SyncAcrossTabsWithoutLocal(typeof(ProductsSlice), PersistStorage.Server),
        ]);

        // An infinite timeout is the longest, never the shortest: both DUCKY316 checks fire under a finite InitTimeout.
        ResolveFails(static d =>
        {
            d.InitTimeout = TimeSpan.FromSeconds(4);
            d.AddBlazor(static o =>
            {
                o.HydrationTimeout = Timeout.InfiniteTimeSpan;
                o.PrerenderSeedWaitTimeout = Timeout.InfiniteTimeSpan;
            });
        }).Errors.ShouldBe([
            BlazorErrors.HydrationTimeoutNotBelowInitTimeout(Timeout.InfiniteTimeSpan, TimeSpan.FromSeconds(4)),
            BlazorErrors.SeedWaitNotBelowHydrationTimeout(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan),
        ]);
    }

    [Fact]
    public void Validation_PersistConfigureThrows_OtherErrorsReportedAndSliceNamed()
    {
        // Non-normative: a throwing configure delegate hides no other error, nor another slice's throw (INV-31), and each
        // failure names its slice.
        var exception = ResolveFails(static d =>
        {
            d.AddSlice<CounterSlice>().AddSlice<TallySlice>().AddSlice<StatusSlice>();
            d.Persist<CounterSlice>(static _ => throw new InvalidOperationException("boom"));
            d.Persist<TallySlice>(static o => o.Version = 2);                          // DUCKY311, between the throwing slices
            d.Persist<StatusSlice>(static _ => throw new InvalidOperationException("bang"));
            d.Prerender<OrphanSlice>();                                               // DUCKY310
            d.AddBlazor(static o => o.HydrationTimeout = TimeSpan.FromMinutes(1));    // DUCKY316
            d.InitTimeout = TimeSpan.FromSeconds(4);
        });

        exception.Errors.ShouldBe([
            BlazorErrors.SliceNotAdded("Prerender", typeof(OrphanSlice)),
            BlazorErrors.HydrationTimeoutNotBelowInitTimeout(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(4)),
            BlazorErrors.MigrationGap(typeof(TallySlice), 2, [1]),
        ]);
        var thrown = exception.InnerException.ShouldBeOfType<AggregateException>().InnerExceptions;
        thrown.Select(static e => e.ShouldBeOfType<InvalidOperationException>().Message)
            .ShouldBe([$"Persist<{typeof(CounterSlice).FullName}> configure threw.", $"Persist<{typeof(StatusSlice).FullName}> configure threw."]);
        thrown.Select(static e => e.InnerException.ShouldBeOfType<InvalidOperationException>().Message).ShouldBe(["boom", "bang"]);
    }

    [Fact]
    public async Task Validation_FinalComposedOptions_NoError()
    {
        // Non-normative: a later delegate fixes what an earlier one broke, so nothing is reported. Every boundary is one
        // tick inside its limit; a complete chain, SyncAcrossTabs on Local storage and the defaults are valid.
        await using var provider = Build(static d =>
        {
            d.InitTimeout = TimeSpan.FromSeconds(1);
            d.AddSlice<CounterSlice>().AddSlice<TallySlice>().AddSlice<StatusSlice>().AddSlice<RatioSlice>();
            d.PersistAttributeDefaults<CounterSlice>(static o => o.Version = 3);
            d.Persist<CounterSlice>(static o => o.Migrate(1, Same).Migrate(2, Same));
            d.Persist<TallySlice>(static o => o.SyncAcrossTabs = true);
            d.Persist<StatusSlice>(static o =>
            {
                o.Storage = PersistStorage.Server;
                o.SyncAcrossTabs = true;
            });
            d.Persist<StatusSlice>(static o => o.SyncAcrossTabs = false);
            d.Prerender<RatioSlice>();
            d.AddBlazor(static o =>
            {
                o.HydrationTimeout = TimeSpan.FromSeconds(9);
                o.Scope = _tenantScope;
            });
            d.AddBlazor(static o =>
            {
                o.Scope = _userScope;
                o.HydrationTimeout = TimeSpan.FromSeconds(1) - TimeSpan.FromTicks(1);
                o.PrerenderSeedWaitTimeout = o.HydrationTimeout - TimeSpan.FromTicks(1);
            });
        });
        await using var scope = provider.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<IStore>().ShouldNotBeNull();
        var options = provider.GetRequiredService<BlazorOptions>();
        (new BlazorOptions().HydrationTimeout, new BlazorOptions().PrerenderSeedWaitTimeout).ShouldBe((TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(250)));
        options.HydrationTimeout.ShouldBe(TimeSpan.FromSeconds(1) - TimeSpan.FromTicks(1));
        options.Scope.ShouldBeSameAs(_userScope);
        new BlazorOptions().Scope.ShouldBeNull();

        // An infinite InitTimeout is longer than any HydrationTimeout.
        await using var infinite = Build(static d =>
        {
            d.InitTimeout = Timeout.InfiniteTimeSpan;
            d.AddBlazor();
        });
        await using var infiniteScope = infinite.CreateAsyncScope();
        infiniteScope.ServiceProvider.GetRequiredService<IStore>().ShouldNotBeNull();
    }

    private static readonly Func<IServiceProvider, CancellationToken, ValueTask<string?>> _tenantScope = static (_, _) => ValueTask.FromResult<string?>("tenant");

    private static readonly Func<IServiceProvider, CancellationToken, ValueTask<string?>> _userScope = static (_, _) => ValueTask.FromResult<string?>("user");

    private static JsonNode Same(JsonNode node) => node;

    // A later UseJson replaces PersistJson, the last call wins.
    private static ServiceProvider Build(Action<DuckyBuilder> configure) =>
        new ServiceCollection().AddDucky(d => configure(d.UseJson(PersistJson.Default))).BuildServiceProvider(_validating);

    private static DuckyConfigurationException ResolveFails(Action<DuckyBuilder> configure)
    {
        using var provider = Build(configure);
        using var scope = provider.CreateScope();
        return Should.Throw<DuckyConfigurationException>(() => scope.ServiceProvider.GetRequiredService<IStore>());
    }
}
