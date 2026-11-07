using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Ducky.Blazor;
using Ducky.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;

namespace Ducky.Docs.Tests.Blazor;

#region persistence-state
public sealed record Preferences(string Theme);

public sealed record ThemeSelected(string Theme);

public sealed class PreferencesSlice : Slice<Preferences>
{
    public PreferencesSlice() => On<ThemeSelected>((_, action) => new Preferences(action.Theme));

    // A persisted slice pins its key: it names the storage entry (DUCKY012).
    public override string Key => "preferences";

    protected override Preferences Initial => new("light");
}
#endregion

// The other guides' state types: DUCKY006 asks the context to cover every slice state type of the project.
[JsonSerializable(typeof(Counter))]
[JsonSerializable(typeof(Core.Cart))]
[JsonSerializable(typeof(Core.EffectsGuideTests.TickCount))]
[JsonSerializable(typeof(Core.Greeting))]
[JsonSerializable(typeof(Core.Layout))]
[JsonSerializable(typeof(Core.Rename))]
[JsonSerializable(typeof(Core.Settings))]
[JsonSerializable(typeof(Core.Todos))]
[JsonSerializable(typeof(Core.Weather))]
[JsonSerializable(typeof(CatalogState))]
[JsonSerializable(typeof(ForecastState))]
[JsonSerializable(typeof(Reactive.Search))]
[JsonSerializable(typeof(Reactive.Dashboard))]
[JsonSerializable(typeof(IReadOnlyList<Reactive.OrderDetails>))]
[JsonSerializable(typeof(Draft.Board))]
#region persistence-json
// Persistence serializes each state through its declared type's JsonTypeInfo: trim- and AOT-safe, no reflection.
[JsonSerializable(typeof(Preferences))]
[JsonSerializable(typeof(Basket))]
public sealed partial class DocsJson : JsonSerializerContext;
#endregion

public sealed record Basket(IReadOnlyList<string> Items);

public sealed record AddedToBasket(string Item);

public sealed record SignedOut;

public sealed class BasketSlice : Slice<Basket>
{
    public BasketSlice()
    {
        On<AddedToBasket>((state, action) => new Basket([.. state.Items, action.Item]));
        On<SignedOut>(_ => Initial);
    }

    public override string Key => "basket";

    protected override Basket Initial => new([]);
}

// The snippets of docs/guides/blazor/persistence.md.
public sealed class PersistenceGuideTests
{
    private static class App
    {
        #region persistence-browser
        public static DuckyBuilder AddPreferences(DuckyBuilder ducky) => ducky
            .UseJson(DocsJson.Default)
            .AddSlice<PreferencesSlice>()
            .Persist<PreferencesSlice>(); // localStorage, key "ducky:preferences"
        #endregion

        #region persistence-version
        public static DuckyBuilder AddPreferencesV2(DuckyBuilder ducky) => ducky
            .UseJson(DocsJson.Default)
            .AddSlice<PreferencesSlice>()
            .Persist<PreferencesSlice>(options =>
            {
                options.Version = 2; // version 1 stored {"Dark": true}; version 2 stores {"Theme": "dark"}
                options.Migrate(1, v1 => new JsonObject { ["Theme"] = v1["Dark"]!.GetValue<bool>() ? "dark" : "light" });
                options.MaxAge = TimeSpan.FromDays(30); // older entries are discarded
            });
        #endregion

        #region persistence-status
        public static bool IsHydrating(StateSnapshot state) =>
            state.Get<PersistenceState>().Status == PersistenceStatus.Hydrating;
        #endregion

        #region persistence-clear
        // Sign-out: reset the slices that hold the user's data (BasketSlice handles SignedOut), then remove their stored
        // entries. Reset first, then clear: the clear drops the reset's pending write and removes the entries; a reset
        // dispatched after the clear would write them again.
        public static async Task SignOut(IStore store)
        {
            await store.DispatchAsync(new SignedOut());
            await store.DispatchAsync(new ClearPersistedState());
        }
        #endregion
    }

    // Non-normative (M15-03): a change is written to localStorage under {prefix}:{sliceKey}, and the next store restores it.
    [Fact]
    public async Task Persist_Local_WrittenAndRestored()
    {
        var browser = new FakeBrowser();
        await using (var store = Create(App.AddPreferences, browser))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            App.IsHydrating(store.State).ShouldBeFalse();
            store.Dispatch(new ThemeSelected("dark"));
            await store.Settled();
        }

        Payload(browser.Storage[("local", "ducky:preferences")]).ShouldBe("""{"Theme":"dark"}""");
        await using var next = Create(App.AddPreferences, browser);
        await next.InitializeAsync(TestContext.Current.CancellationToken);
        next.State.Get<Preferences>().ShouldBe(new Preferences("dark"));
        next.State.WasRestored<Preferences>().ShouldBeTrue();
    }

    // Non-normative (M15-03): a version 1 entry is migrated on read; a newer version than the app's is not restored.
    [Theory]
    [InlineData(1, """{"Dark":true}""", "dark")]
    [InlineData(1, """{"Dark":false}""", "light")]
    [InlineData(3, """{"Theme":"dark"}""", "light")]
    public async Task Persist_Version_MigratesOlderEntries(int version, string stored, string theme)
    {
        var browser = new FakeBrowser();
        var logs = new FakeLogCollector();
        browser.Storage[("local", "ducky:preferences")] = $$"""{"v":{{version}},"at":"2000-01-01T00:00:00Z","s":{{stored}}}""";
        await using var store = Create(App.AddPreferencesV2, browser, logs);

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        store.State.Get<Preferences>().ShouldBe(new Preferences(theme));
        store.State.WasRestored<Preferences>().ShouldBe(version == 1);
        // EventId 2031: the envelope is newer than the app's version, read as not found (not a parse failure).
        logs.GetSnapshot().Any(static record => record.Id.Id == 2031 && record.Level == LogLevel.Warning).ShouldBe(version == 3);
    }

    // Non-normative (M15-03): the persistence-status snippet reads Hydrating while the hydration read is pending.
    [Fact]
    public async Task Persist_Status_HydratingUntilTheReadCompletes()
    {
        var reads = new TaskCompletionSource();
        var browser = new FakeBrowser { ReadsHeld = reads.Task };
        await using var store = Create(App.AddPreferences, browser);

        var initialization = store.InitializeAsync(TestContext.Current.CancellationToken);
        browser.CallsTo("storageGet").ShouldNotBeEmpty(); // the read is issued, and held

        App.IsHydrating(store.State).ShouldBeTrue();
        reads.SetResult();
        await initialization;
        App.IsHydrating(store.State).ShouldBeFalse();
        store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Hydrated);
    }

    // Non-normative (M15-03): an entry older than MaxAge is discarded.
    [Fact]
    public async Task Persist_MaxAge_DiscardsOldEntries()
    {
        var browser = new FakeBrowser();
        browser.Storage[("local", "ducky:preferences")] = """{"v":2,"at":"2000-01-01T00:00:00Z","s":{"Theme":"dark"}}""";
        await using var store = Create(App.AddPreferencesV2, browser);
        store.Time.Advance(TimeSpan.FromDays(31)); // the fake clock starts at 2000-01-01

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        store.State.Get<Preferences>().ShouldBe(new Preferences("light"));
    }

    // Non-normative (M15-03): the server sample keeps each user's basket in IDistributedCache under {prefix}:{user}:{key};
    // another user never sees it, an anonymous request restores nothing and never falls back to a shared key, and the sign-out handler clears it.
    [Fact]
    public async Task Persist_Server_PerUserKeys()
    {
        await using var app = ServerApp();
        var cache = app.Services.GetRequiredService<IDistributedCache>();

        await using (var alice = await Request(app, "alice"))
        {
            alice.Dispatch(new AddedToBasket("duck-yellow"));
        } // the request scope's dispose flushes the write

        Payload(Encoding.UTF8.GetString((await cache.GetAsync("shop:alice:basket", TestContext.Current.CancellationToken))!)).ShouldBe("""{"Items":["duck-yellow"]}""");
        await using (var bob = await Request(app, "bob"))
        {
            bob.State.Get<Basket>().Items.ShouldBeEmpty();
        }

        await using (var anonymous = await Request(app, user: null))
        {
            anonymous.Dispatch(new AddedToBasket("duck-blue"));
        }

        await using (var anonymous = await Request(app, user: null))
        {
            anonymous.State.Get<Basket>().Items.ShouldBeEmpty(); // a shared fallback key would hand back duck-blue
            anonymous.State.WasRestored<Basket>().ShouldBeFalse();
        }

        (await cache.GetAsync("shop:basket", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await cache.GetAsync("shop::basket", TestContext.Current.CancellationToken)).ShouldBeNull();
        await using (var alice = await Request(app, "alice"))
        {
            alice.State.Get<Basket>().Items.ShouldBe(["duck-yellow"]);
            await App.SignOut(alice.Store);
            alice.State.Get<Basket>().Items.ShouldBeEmpty();
        }

        (await cache.GetAsync("shop:alice:basket", TestContext.Current.CancellationToken)).ShouldBeNull();
    }


    // Non-normative (M15-03): a Scope delegate replaces the user segment, here with a tenant claim.
    [Fact]
    public async Task Persist_Scope_TenantSegment()
    {
        await using var app = ServerApp(options =>
        {
            #region persistence-scope
            options.Scope = static async (services, cancellationToken) =>
            {
                var authentication = await services.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();
                return authentication.User.FindFirst("tenant")?.Value; // null: nothing is read or written
            };
            #endregion
        });

        await using (var alice = await Request(app, "alice", tenant: "acme"))
        {
            alice.Dispatch(new AddedToBasket("duck-yellow"));
        }

        await using var bob = await Request(app, "bob", tenant: "acme");
        bob.State.Get<Basket>().Items.ShouldBe(["duck-yellow"]);
        (await app.Services.GetRequiredService<IDistributedCache>().GetAsync("shop:acme:basket", TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static TestStore Create(Func<DuckyBuilder, DuckyBuilder> configure, FakeBrowser browser, FakeLogCollector? logs = null) =>
        TestStore.Create(d => configure(d), services => services
            .AddSingleton<IJSRuntime>(browser)
            .AddLogging(logging => logging.AddProvider(new FakeLoggerProvider(logs ?? new FakeLogCollector()))));

    // The envelope's state: {"v":…,"at":…,"s":<state>}.
    private static string Payload(string envelope) => JsonNode.Parse(envelope)!["s"]!.ToJsonString();

    private static WebApplication ServerApp(Action<BlazorOptions>? configure = null)
    {
        #region persistence-server
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddDistributedMemoryCache(); // AddStackExchangeRedisCache(...) in production
        builder.Services.AddDucky(ducky => ducky
            .UseJson(DocsJson.Default)
            .AddSlice<BasketSlice>()
            .AddBlazor(options => options.KeyPrefix = "shop") // app-unique: the cache may be shared (DUCKY317)
            .Persist<BasketSlice>(options => options.Storage = PersistStorage.Server));
        #endregion
        // AddDucky runs once per container (DUCKY300): a test's options go to the instance AddBlazor registered.
        configure?.Invoke((BlazorOptions)builder.Services.Single(static d => d.ServiceType == typeof(BlazorOptions)).ImplementationInstance!);

        return builder.Build();
    }

    // One request: its scope, its user (as the framework sets it), its store, initialized.
    private static async Task<ScopedStore> Request(WebApplication app, string? user, string? tenant = null)
    {
        var scope = app.Services.CreateAsyncScope();
        Claim[] claims = [.. user is null ? [] : new[] { new Claim(ClaimTypes.NameIdentifier, user) }, .. tenant is null ? [] : new[] { new Claim("tenant", tenant) }];
        var principal = new ClaimsPrincipal(user is null ? new ClaimsIdentity() : new ClaimsIdentity(claims, "test"));
        ((IHostEnvironmentAuthenticationStateProvider)scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(principal)));
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        return new(scope, store);
    }

    private sealed class ScopedStore(AsyncServiceScope scope, IStore store) : IAsyncDisposable
    {
        public IStore Store => store;

        public StateSnapshot State => store.State;

        public void Dispatch(object action) => store.Dispatch(action);

        public ValueTask DisposeAsync() => scope.DisposeAsync();
    }
}
