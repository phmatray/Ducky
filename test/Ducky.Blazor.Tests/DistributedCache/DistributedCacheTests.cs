using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Blazor.Tests.DistributedCache;

// SPEC §11.6 (PersistStorage.Server over IDistributedCache: keys {prefix}:{scope}:{sliceKey}, Scope ?? NameIdentifier for
// Server keys only, a null scope means no I/O, inert in the browser, the NonInteractive rule, debounce 0), §11.1
// (DUCKY314, DUCKY315, DUCKY317); INV-17, INV-31. Every store runs over the real MemoryDistributedCache, and every
// NonInteractive one also over a wrapper whose GetAsync first yields, as Redis does.
public sealed class DistributedCacheTests
{
    private const int NullScopeEventId = 2027;
    private static readonly ServiceProviderOptions _validating = new() { ValidateScopes = true, ValidateOnBuild = true };
    private static readonly Action<PersistOptions> _server = static o => o.Storage = PersistStorage.Server;
    private readonly FakeLogCollector _logs = new();
    private int _probes; // interop calls of the NonInteractive stores, each refused as the prerender runtime does

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistributedCache_TwoScopes_NeverSeeEachOther(bool yield)
    {
        // Request stores (NonInteractive: the prerender JS runtime) of two users over one cache, under the default
        // NameIdentifier scope: each reads and writes only {prefix}:{user}:{sliceKey}.
        var cache = new TestCache(yield);
        await using (var alice = NonInteractive(cache, "alice"))
        {
            await alice.InitializeAsync();
            alice.Store.Dispatch(new Increment());
            await WriterHarness.Until(() => cache.Stored("app:alice:counter") is not null);
            alice.Store.Dispatch(new Increment());
            await WriterHarness.Until(() => Payload(cache.Stored("app:alice:counter")!) == """{"Value":2}""");
        }

        await using (var bob = NonInteractive(cache, "bob"))
        {
            await bob.InitializeAsync();

            // Alice's value is not Bob's: nothing restored for him.
            bob.Store.State.Get<Counter>().ShouldBe(new Counter(0));
            bob.Log.Entries.ShouldBe(["restore:@ducky/persistence Hydrating:0", "completed:False:0:System Hydrated:0", "initialized Hydrated:0"]);
            bob.Store.Dispatch(new Increment());
            await WriterHarness.Until(() => cache.Stored("app:bob:counter") is not null);
        }

        Payload(cache.Stored("app:alice:counter")!).ShouldBe("""{"Value":2}""");
        Payload(cache.Stored("app:bob:counter")!).ShouldBe("""{"Value":1}""");
        cache.Calls.ShouldBe(["get:app:alice:counter", "set:app:alice:counter", "set:app:alice:counter", "get:app:bob:counter", "set:app:bob:counter"]);

        // Each user's next store restores their own value, and a clear removes only the clearing user's key.
        await using (var alice = NonInteractive(cache, "alice"))
        {
            await alice.InitializeAsync();
            alice.Store.State.Get<Counter>().ShouldBe(new Counter(2));
            alice.Store.Dispatch(new ClearPersistedState());

            // The removal is delivered, not left pending for a retry.
            await WriterHarness.Until(() => !alice.Middleware.Writers["counter"].RemovalPending && cache.Stored("app:alice:counter") is null);
        }

        await using (var bob = NonInteractive(cache, "bob"))
        {
            await bob.InitializeAsync();
            bob.Store.State.Get<Counter>().ShouldBe(new Counter(1));
        }

        cache.Calls.Skip(5).ShouldBe(["get:app:alice:counter", "remove:app:alice:counter", "get:app:bob:counter"]);

        // A store holding only Server storage never probes the gate, and logs neither the browser's inert storage nor a
        // null scope.
        _logs.GetSnapshot().Select(static r => r.Id.Id).ShouldNotContain(2020);
        _logs.GetSnapshot().Select(static r => r.Id.Id).ShouldNotContain(NullScopeEventId);
        _probes.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "")]
    [InlineData(true, " ")]
    public async Task DistributedCache_NullScope_NoIo(bool yield, string? resolved)
    {
        // A Scope delegate that returns null: no read, no write, no removal, never a fallback to a shared key; one Info log.
        // An empty or whitespace scope is null too, never the shared key {prefix}::{sliceKey}.
        var cache = new TestCache(yield);
        await using var store = NonInteractive(cache, "alice", d => d.AddBlazor(o => o.Scope = (_, _) => ValueTask.FromResult(resolved)));

        await store.InitializeAsync();
        store.Store.Dispatch(new Increment());
        store.Store.Dispatch(new ClearPersistedState());
        await store.Store.WhenIdleAsync(Ct);
        await store.IterationsAsync("counter", 1);
        store.Store.State.Get<Counter>().ShouldBe(new Counter(1));
        await store.DisposeStoreAsync();

        cache.Calls.ShouldBeEmpty();
        store.Log.Entries.Take(3).ShouldBe(["restore:@ducky/persistence Hydrating:0", "completed:False:0:System Hydrated:0", "initialized Hydrated:0"]);
        store.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = null });
        var info = _logs.GetSnapshot().Where(static r => r.Id.Id == NullScopeEventId).ShouldHaveSingleItem();
        info.Level.ShouldBe(LogLevel.Information);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "")]
    [InlineData(true, " ")]
    public async Task DistributedCache_AnonymousUser_NoIo(bool yield, string? user)
    {
        // The default scope, DuckyScopes.NameIdentifier, resolves an anonymous user to null: no I/O for Server storage. So
        // does a principal whose NameIdentifier claim is empty or whitespace.
        var cache = new TestCache(yield);
        await using var store = NonInteractive(cache, user);

        await store.InitializeAsync();
        store.Store.Dispatch(new Increment());
        await store.IterationsAsync("counter", 1);
        await store.DisposeStoreAsync();

        cache.Calls.ShouldBeEmpty();
        store.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = null });
        _logs.GetSnapshot().Where(static r => r.Id.Id == NullScopeEventId).ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Information);

        // The delegate itself: the NameIdentifier claim of the authentication state, null without one.
        await using var services = new ServiceCollection().AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(null)).BuildServiceProvider();
        (await DuckyScopes.NameIdentifier(services, Ct)).ShouldBeNull();
        await using var signedIn = new ServiceCollection().AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider("alice")).BuildServiceProvider();
        (await DuckyScopes.NameIdentifier(signedIn, Ct)).ShouldBe("alice");
    }

    [Fact]
    public void DistributedCache_DefaultPrefixWithServerStorage_IsConfigurationError()
    {
        // DUCKY317 on both sides (the prefix is shared configuration), once per Server-storage slice; never for browser
        // storage, and never once the prefix is app-unique.
        foreach (var browser in new[] { false, true })
        {
            ResolveFails(Cache, d => d
                .AddSlice<CounterSlice>().AddSlice<DialSlice>().AddSlice<LevelSlice>()
                .Persist<CounterSlice>(_server).Persist<DialSlice>().Persist<LevelSlice>(_server)
                .AddBlazor(o => o.IsBrowser = browser))
                .Errors.ShouldBe([BlazorErrors.DefaultKeyPrefix(typeof(CounterSlice)), BlazorErrors.DefaultKeyPrefix(typeof(LevelSlice))]);
        }

        using var valid = Build(Cache, static d => d.AddSlice<CounterSlice>().Persist<CounterSlice>(_server).AddBlazor(static o => o.KeyPrefix = "app"));
        using var scope = valid.CreateScope();
        scope.ServiceProvider.GetRequiredService<IStore>().ShouldNotBeNull();
        new BlazorOptions().KeyPrefix.ShouldBe("ducky");
        new BlazorOptions().ServerEntryOptions.ShouldBeNull();

        static void Cache(IServiceCollection services) => services.AddSingleton<IDistributedCache>(new TestCache(yield: false)).AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider("alice"));
    }

    [Fact]
    public async Task Browser_ServerStorageWithoutCacheOrAuth_NoConfigurationError()
    {
        // The shared Interactive Auto registration on the WASM side: neither IDistributedCache nor an
        // AuthenticationStateProvider, so DUCKY314 and DUCKY315 are not evaluated. Server storage is inert there.
        await using var provider = Build(static _ => { }, static d => d
            .AddSlice<CounterSlice>().Persist<CounterSlice>(_server).AddBlazor(static o => (o.IsBrowser, o.KeyPrefix) = (true, "app")));
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();

        await store.InitializeAsync(Ct);
        store.Dispatch(new Increment());

        store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Hydrated);
        scope.ServiceProvider.GetRequiredService<PersistenceSlice>().Middleware.ShouldNotBeNull().Writers.ShouldBeEmpty();
    }

    [Fact]
    public void Server_ServerStorageWithoutCache_Ducky314()
    {
        // The server twin: DUCKY314 without a cache; DUCKY315 too when the default scope has no AuthenticationStateProvider.
        static void Auth(IServiceCollection services) => services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider("alice"));
        static void App(DuckyBuilder d) => d.AddSlice<CounterSlice>().AddSlice<DialSlice>().Persist<CounterSlice>(_server).Persist<DialSlice>().AddBlazor(static o => o.KeyPrefix = "app");

        ResolveFails(Auth, App).Errors.ShouldBe([BlazorErrors.NoDistributedCache(typeof(CounterSlice))]);
        ResolveFails(static _ => { }, App).Errors.ShouldBe([BlazorErrors.NoDistributedCache(typeof(CounterSlice)), BlazorErrors.NoAuthenticationStateProvider(typeof(CounterSlice))]);

        // An explicit Scope needs no AuthenticationStateProvider; with a cache, nothing is reported.
        ResolveFails(static _ => { }, static d => App(d.AddBlazor(static o => o.Scope = static (_, _) => ValueTask.FromResult<string?>("tenant"))))
            .Errors.ShouldBe([BlazorErrors.NoDistributedCache(typeof(CounterSlice))]);
        ResolveFails(static services => services.AddSingleton<IDistributedCache>(new TestCache(yield: false)), App)
            .Errors.ShouldBe([BlazorErrors.NoAuthenticationStateProvider(typeof(CounterSlice))]);

        // Keyed registrations are not the unkeyed services Server storage resolves: both are still reported.
        ResolveFails(static services => services
                .AddKeyedSingleton<IDistributedCache>("x", new TestCache(yield: false))
                .AddKeyedSingleton<AuthenticationStateProvider>("x", new TestAuthenticationStateProvider("alice")), App)
            .Errors.ShouldBe([BlazorErrors.NoDistributedCache(typeof(CounterSlice)), BlazorErrors.NoAuthenticationStateProvider(typeof(CounterSlice))]);
        using var valid = Build(static services => Auth(services.AddSingleton<IDistributedCache>(new TestCache(yield: false))), App);
        using var scope = valid.CreateScope();
        scope.ServiceProvider.GetRequiredService<IStore>().ShouldNotBeNull();
    }

    [Fact]
    public async Task Scope_DefaultNeverAppliesToBrowserKeys()
    {
        // A circuit (Interactive: the probe's import succeeds) with no Scope: the Server key takes the default NameIdentifier
        // scope, the Local key stays unscoped. Server storage is written through (debounce 0), with no timer.
        var cache = new TestCache(yield: false);
        cache.Store("app:alice:level", Envelope("""{"Value":2.5}"""));
        await using (var circuit = Circuit(cache, "alice"))
        {
            circuit.Storage[("local", "app:dial")] = Envelope("""{"Value":4}""");
            await circuit.InitializeAsync();

            (circuit.Store.State.Get<Level>(), circuit.Store.State.Get<Dial>()).ShouldBe((new Level(2.5), new Dial(4)));
            circuit.Js.Calls.Where(static c => c.Identifier == "storageGet").Select(static c => c.Args[1]).ShouldBe(["app:dial"]);
            circuit.Store.Dispatch(new SetLevel(3));
            circuit.Store.Dispatch(new SetDial(5));
            (await circuit.NextWriteAsync()).Key.ShouldBe("app:dial");
            await WriterHarness.Until(() => Payload(cache.Stored("app:alice:level")!) == """{"Value":3}""");
            circuit.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = "alice" });
        }

        cache.Calls.ShouldBe(["get:app:alice:level", "set:app:alice:level"]);

        // An anonymous user keeps browser persistence: the Server slice does no I/O, the Local key is read and written.
        await using (var anonymous = Circuit(cache, user: null))
        {
            await anonymous.InitializeAsync();
            anonymous.Store.Dispatch(new SetLevel(9));
            anonymous.Store.Dispatch(new SetDial(6));
            (await anonymous.NextWriteAsync()).Key.ShouldBe("app:dial");
            await anonymous.IterationsAsync("level", 1);
            anonymous.Js.Calls.Where(static c => c.Identifier == "storageGet").Select(static c => c.Args[1]).ShouldBe(["app:dial"]);
        }

        cache.Calls.Count.ShouldBe(2);

        // In the browser no scope is in use without an explicit Scope: the Local key is unscoped, Server storage inert, and
        // nothing asks for an AuthenticationStateProvider.
        await using var wasm = new WriterHarness(App, browser: true, services: _ => { });
        await wasm.InitializeAsync();
        wasm.Js.Calls.Where(static c => c.Identifier == "storageGet").Select(static c => c.Args[1]).ShouldBe(["app:dial"]);
        wasm.Middleware.Scopes.ShouldBeEmpty();
        wasm.Middleware.Writers.Keys.ShouldBe(["dial"]);

        static void App(DuckyBuilder d) => d.Persist<LevelSlice>(_server).Persist<DialSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(static o => o.KeyPrefix = "app");
        WriterHarness Circuit(TestCache cache, string? user) => new(App, browser: false, services: s => Services(s, cache, user));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistributedCache_EntryOptions_MaxAgeElseServerEntryOptions(bool yield)
    {
        // (non-normative) §11.6: MaxAge sets AbsoluteExpirationRelativeToNow; otherwise ServerEntryOptions applies, and
        // without them the entry never expires. The value is the UTF-8 envelope.
        var cache = new TestCache(yield);
        var entry = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(20) };
        await using (var store = NonInteractive(cache, "alice", d => d
            .Persist<LevelSlice>(static o => (o.Storage, o.MaxAge) = (PersistStorage.Server, TimeSpan.FromHours(1))).Persist<DialSlice>(_server)
            .AddBlazor(o => o.ServerEntryOptions = entry)))
        {
            await store.InitializeAsync();
            store.Store.Dispatch(new SetLevel(1));
            store.Store.Dispatch(new SetDial(2));
            await WriterHarness.Until(() => cache.Entries.Count == 2);
        }

        cache.Entries["app:alice:level"].AbsoluteExpirationRelativeToNow.ShouldBe(TimeSpan.FromHours(1));
        cache.Entries["app:alice:dial"].ShouldBeSameAs(entry);
        Payload(cache.Stored("app:alice:dial")!).ShouldBe("""{"Value":2}""");

        await using (var store = NonInteractive(cache, "bob"))
        {
            await store.InitializeAsync();
            store.Store.Dispatch(new Increment());
            await WriterHarness.Until(() => cache.Entries.ContainsKey("app:bob:counter"));
        }

        var plain = cache.Entries["app:bob:counter"];
        (plain.AbsoluteExpiration, plain.AbsoluteExpirationRelativeToNow, plain.SlidingExpiration).ShouldBe((null, null, null));

        // A MaxAge of zero (or less) expires the entry at once: the write removes the key, where a non-positive
        // AbsoluteExpirationRelativeToNow would throw on every attempt.
        cache.Store("app:carol:counter", Envelope("""{"Value":7}"""));
        await using (var store = NonInteractive(cache, "carol", static d => d.Persist<CounterSlice>(static o => (o.Storage, o.MaxAge) = (PersistStorage.Server, TimeSpan.Zero))))
        {
            await store.InitializeAsync();
            store.Store.Dispatch(new Increment());
            await store.IterationsAsync("counter", 1);
        }

        cache.Calls.Where(static c => c.EndsWith(":app:carol:counter", StringComparison.Ordinal)).ShouldBe(["get:app:carol:counter", "remove:app:carol:counter"]);
        cache.Stored("app:carol:counter").ShouldBeNull();
        _logs.GetSnapshot().ShouldNotContain(static r => r.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistributedCache_NonInteractive_ServerReadBrowserSkipped(bool yield)
    {
        // (non-normative) §11.5 step 1, §11.6: a prerender or static SSR store reads Server storage during init and writes it
        // through, while its browser-storage slices are skipped (no read, no writer).
        var cache = new TestCache(yield);
        cache.Store("app:alice:counter", Envelope("""{"Value":7}"""));
        await using var store = NonInteractive(cache, "alice", static d => d.Persist<DialSlice>());

        await store.InitializeAsync();
        store.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => Payload(cache.Stored("app:alice:counter")!) == """{"Value":8}""");

        store.Log.Entries.Take(4).ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
        store.Js.Calls.Select(static c => c.Identifier).ShouldBe(["import"]); // the probe, never a read
        store.Middleware.Writers.Keys.ShouldBe(["counter"]);
    }

    private static string Payload(string envelope) => System.Text.Json.JsonDocument.Parse(envelope).RootElement.GetProperty("s").GetRawText();

    private static string Envelope(string payload) => EnvelopeWriter.Write(payload, version: 1, new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

    // A request store whose JS runtime is the prerender one (every call throws synchronously), counter in Server storage.
    private WriterHarness NonInteractive(TestCache cache, string? user, Action<DuckyBuilder>? more = null)
    {
        var harness = new WriterHarness(
            d =>
            {
                d.Persist<CounterSlice>(_server).AddBlazor(static o => o.KeyPrefix = "app");
                more?.Invoke(d);
            },
            browser: false,
            services: s => Services(s, cache, user));
        harness.Js.Respond = (_, _) =>
        {
            Interlocked.Increment(ref _probes);
            throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");
        };
        return harness;
    }

    private void Services(IServiceCollection services, TestCache cache, string? user) => services
        .AddSingleton<IDistributedCache>(cache)
        .AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(user))
        .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs)));

    private static ServiceProvider Build(Action<IServiceCollection> services, Action<DuckyBuilder> configure)
    {
        var collection = new ServiceCollection();
        services(collection);
        return collection.AddDucky(d => configure(d.UseJson(WriterJson.Default))).BuildServiceProvider(_validating);
    }

    private static DuckyConfigurationException ResolveFails(Action<IServiceCollection> services, Action<DuckyBuilder> configure)
    {
        using var provider = Build(services, configure);
        using var scope = provider.CreateScope();
        return Should.Throw<DuckyConfigurationException>(() => scope.ServiceProvider.GetRequiredService<IStore>());
    }
}
