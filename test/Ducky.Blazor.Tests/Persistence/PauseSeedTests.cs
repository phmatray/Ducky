using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Bunit;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Persistence;

// SPEC §11.4 pause seeds, §11.5 step 6; INV-15. A circuit paused by .NET 10 writes a seed (src "pause") from one snapshot:
// its values, the browser-storage keys whose serialization differs from the last known stored payload (dirty), and the
// PersistOptions.Version of every persisted key it carries (ver). On resume only a dirty key overrides storage (read for its
// baseline, restore skipped, rewritten after the terminal); a clean key follows prerender precedence, and a key whose
// recorded version differs or is missing is never applied from the seed. Each seed comes from a real paused circuit (a
// second BunitContext over the same storage); storage is ducky.js behind a FakeJsRuntime (§17.2).
public sealed class PauseSeedTests : BunitContext
{
    private const string Key = "ducky:counter";
    private static readonly RendererInfo _server = new("Server", isInteractive: true);
    private readonly FakeJsRuntime _js = new();
    private readonly Dictionary<(string Area, string Key), object?> _storage = [];
    private readonly Channel<string> _writes = Channel.CreateUnbounded<string>();
    private readonly HydrationLog _log = new();
    private readonly FakeLogCollector _logs = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
    private bool _disconnected;
    private bool _readHangs;
    private string? _user;

    public PauseSeedTests() => _js.Respond = (identifier, args) => identifier switch
    {
        "import" => _js,
        "storageGet" => _readHangs ? new TaskCompletionSource<object?>().Task : _storage.GetValueOrDefault(((string)args[0]!, (string)args[1]!)),
        "storageSet" => Set((string)args[0]!, (string)args[1]!, (string)args[2]!),
        _ => null,
    };

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private IStore Store => Services.GetRequiredService<IStore>();

    [Fact]
    public async Task PauseSeed_DirtyKeysSkipStorageRestoreAndAreRewritten()
    {
        // (non-normative) The circuit read 3, the user made it 4, and the write failed on the disconnect that preceded the
        // pause: storage still holds 3, so the seed lists the key as dirty.
        _storage[("local", Key)] = Envelope(3, version: 1);
        var seed = await PauseAsync(increments: 1, disconnected: true);
        var envelope = Seed(seed);
        envelope["src"]!.GetValue<string>().ShouldBe("pause");
        envelope["dirty"]!.AsArray().Select(static key => key!.GetValue<string>()).ShouldBe(["counter"]);
        envelope["ver"]!["counter"]!.GetValue<int>().ShouldBe(1);

        // The resumed circuit still reads the key (its baseline is 3), keeps the seed's 4 over the stale 3, and rewrites
        // storage once the terminal released the key.
        Configure();
        await ResumeAsync(seed);
        await Store.InitializeAsync(Ct);

        Payload(await NextWriteAsync()).ShouldBe("""{"Value":4}""");
        Store.State.Get<Counter>().ShouldBe(new Counter(4));
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        _log.Entries.ShouldBe([
            "restore:counter Hydrated:0",
            "restore:@ducky/persistence Hydrating:0",
            "completed:False:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
    }

    [Fact]
    public async Task Circuit_ResumeAfterOtherTabWrote_CleanKey_StorageWins()
    {
        // The circuit wrote its 4 before the pause, so the key is clean. While it was paused another tab wrote 9: the
        // resumed circuit renders the seed's 4 at once, then storage wins, and nothing rolls the other tab's write back.
        _storage[("local", Key)] = Envelope(3, version: 1);
        var seed = await PauseAsync(increments: 1, disconnected: false);
        Seed(seed)["dirty"].ShouldBeNull();
        _storage[("local", Key)] = Envelope(9, version: 1);

        Configure();
        await ResumeAsync(seed);
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(9));
        _log.Entries.ShouldBe([
            "restore:counter Hydrated:0",
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
        await AssertNothingWrittenAsync(Envelope(9, version: 1));
    }

    [Fact]
    public async Task Circuit_ResumeAfterVersionBump_DirtySeedKeyDropped_StorageMigrated()
    {
        // The circuit paused with a dirty 4 under version 1; the app was redeployed with version 2, whose migration turns
        // a stored v1 state into ten times its value. The seed's old-shape value never bypasses that migration: the key is
        // dropped from the seed restore and from the dirty set (Debug 2015), storage is read and migrated, and the seed
        // value is never written back as a v2 envelope.
        _storage[("local", Key)] = Envelope(3, version: 1);
        var seed = await PauseAsync(increments: 1, disconnected: true);
        Seed(seed)["dirty"]!.AsArray().Count.ShouldBe(1);

        Configure(static o => o.Version = 2, static o => o.Migrate(1, static node => new JsonObject { ["Value"] = node["Value"]!.GetValue<int>() * 10 }));
        await ResumeAsync(seed);
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(30));
        _log.Entries.ShouldBe([
            "restore: Hydrated:0",
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
        await AssertNothingWrittenAsync(Envelope(3, version: 1));
        var dropped = _logs.GetSnapshot().Where(static r => r.Id.Id == 2015).ShouldHaveSingleItem();
        dropped.Level.ShouldBe(LogLevel.Debug);
        dropped.Message.ShouldContain("'counter'");
    }

    [Fact]
    public async Task PauseSeed_PersistedKeyWithoutVersion_Dropped()
    {
        // (non-normative) A seed that records no version for a persisted key (written before the version map, or edited)
        // is treated as skewed: storage applies, and the key is not dirty any more.
        _storage[("local", Key)] = Envelope(3, version: 1);
        Configure();
        await ResumeAsync(Forged("""{"v":1,"src":"pause","dirty":["counter"],"slices":{"counter":{"Value":4}}}"""));
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(3));
        _logs.GetSnapshot().Count(static r => r.Id.Id == 2015).ShouldBe(1);
        await AssertNothingWrittenAsync(Envelope(3, version: 1));
    }

    [Theory]
    [InlineData("""{"v":1,"src":"pause","dirty":[null],"ver":{"counter":1},"slices":{"counter":{"Value":4}}}""", "restore:counter Hydrated:0")]
    [InlineData("""{"v":1,"src":"pause","dirty":["counter"],"ver":{"counter":1},"slices":{"counter":{"Value":"four"}}}""", "restore: Hydrated:0")]
    public async Task PauseSeed_UnusableDirtyEntry_StorageRestoredAndNotOverwritten(string seed, string seedRestore)
    {
        // (non-normative) A null dirty entry is no key (the seed is still restored), and a dirty value the store can't
        // restore (a state type changed without a Version bump) is not a value the seed restored (§11.5 step 6): storage
        // applies, and nothing overwrites it.
        _storage[("local", Key)] = Envelope(3, version: 1);

        Configure();
        await ResumeAsync(Forged(seed));
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(3));
        _log.Entries[0].ShouldBe(seedRestore);
        await AssertNothingWrittenAsync(Envelope(3, version: 1));
    }

    [Fact]
    public async Task PauseSeed_ResumeUnderOtherUser_DirtyScopedKeyNotWrittenUnderNewUser()
    {
        // (non-normative, INV-15) Alice's circuit paused with a dirty 4; Bob signed in on another tab during the disconnect and
        // resumes it. Scoped keys never take the "pause seed wins" path before the scope-hash hand-off (M12-07): Bob's
        // storage is restored, and Alice's value is never written under Bob's key.
        _storage[("local", "ducky:alice:counter")] = Envelope(3, version: 1);
        _storage[("local", "ducky:bob:counter")] = Envelope(9, version: 1);
        _user = "alice";
        var seed = await PauseAsync(increments: 1, disconnected: true);
        Seed(seed)["dirty"]!.AsArray().Count.ShouldBe(1);

        _user = "bob";
        Configure();
        await ResumeAsync(seed);
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(9));
        await Store.DisposeAsync();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
        _storage[("local", "ducky:bob:counter")].ShouldBe(Envelope(9, version: 1));
        _storage[("local", "ducky:alice:counter")].ShouldBe(Envelope(3, version: 1));
    }

    [Fact]
    public async Task PauseSeed_ServerStorageKey_NeverDirty()
    {
        // (non-normative) The pause override never applies to Server storage (§11.4): its key is versioned, never dirty.
        var seed = await PauseAsync(increments: 1, disconnected: true, static o => o.Storage = PersistStorage.Server);

        var envelope = Seed(seed);
        envelope["slices"]!["counter"].ShouldNotBeNull();
        envelope["ver"]!["counter"]!.GetValue<int>().ShouldBe(1);
        envelope["dirty"].ShouldBeNull();
    }

    [Fact]
    public async Task PauseSeed_HydrationTimesOut_DirtyKeyStillRewritten()
    {
        // (non-normative) The read hangs past HydrationTimeout: the attempt ends in HydrationFailed, and the seed's dirty 4,
        // already restored by the handoff, is still written after that terminal rather than lost on the next reload.
        _storage[("local", Key)] = Envelope(3, version: 1);
        var seed = await PauseAsync(increments: 1, disconnected: true);

        Configure();
        _readHangs = true;
        await ResumeAsync(seed);
        var init = Store.InitializeAsync(Ct);
        _time.Advance(TimeSpan.FromSeconds(5));
        await init;

        Payload(await NextWriteAsync()).ShouldBe("""{"Value":4}""");
        Store.State.Get<Counter>().ShouldBe(new Counter(4));
        _log.Entries.ShouldContain("failed:TimeoutException:0:System Failed:0");
    }

    private static FakeComponentStateStore Forged(string envelope)
    {
        var seed = new FakeComponentStateStore();
        seed.State[PrerenderHandoff.SeedKey] = JsonSerializer.SerializeToUtf8Bytes(Encoding.UTF8.GetBytes(envelope));
        return seed;
    }

    // Writer loops run outside quiescence: dispose flushes every writer and released deferred key (phase 5a) first.
    private async Task AssertNothingWrittenAsync(string stored)
    {
        await Store.DisposeAsync();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
        _storage[("local", Key)].ShouldBe(stored);
    }

    private static string Envelope(int value, int version) =>
        EnvelopeWriter.Write($$"""{"Value":{{value}}}""", version, new DateTimeOffset(2026, 9, 30, 11, 0, 0, TimeSpan.Zero));

    private static string Payload(string envelope) => JsonDocument.Parse(envelope).RootElement.GetProperty("s").GetRawText();

    private static JsonNode Seed(FakeComponentStateStore persisted) =>
        JsonNode.Parse(JsonSerializer.Deserialize<byte[]>(persisted.State[PrerenderHandoff.SeedKey]))!;

    private bool Set(string area, string key, string value)
    {
        if (_disconnected)
        {
            throw new JSDisconnectedException("The circuit is disconnected.");
        }

        _storage[(area, key)] = value;
        _writes.Writer.TryWrite(value);
        return true;
    }

    private Task<string> NextWriteAsync() => _writes.Reader.ReadAsync(Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), Ct);

    // The circuit before the pause: it hydrates from storage, the user increments, and .NET 10 pauses it, persisting its
    // state through every registration. With `disconnected`, every write fails (the disconnect that preceded the pause).
    private async Task<FakeComponentStateStore> PauseAsync(int increments, bool disconnected, Action<PersistOptions>? persist = null)
    {
        _disconnected = disconnected;
        var persisted = new FakeComponentStateStore();
        await using (var circuit = new BunitContext())
        {
            AddDucky(circuit, persist ?? (static _ => { }));
            circuit.Render<CounterView>();
            var store = circuit.Services.GetRequiredService<IStore>();
            await store.InitializeAsync(Ct);
            for (var i = 0; i < increments; i++)
            {
                store.Dispatch(new Increment());
            }

            if (!disconnected)
            {
                // Written, and its baseline set: the writer is clean once the write returned.
                await NextWriteAsync();
                var writer = circuit.Services.GetRequiredService<PersistenceSlice>().Middleware.ShouldNotBeNull().Writers["counter"];
                await WriterHarness.Until(() => !writer.Dirty);
            }

            await circuit.Services.GetRequiredService<ComponentStatePersistenceManager>().PersistStateAsync(persisted, circuit.Renderer);
        }

        _disconnected = false;
        return persisted;
    }

    // The resumed circuit: a fresh store whose persistent state the framework restores from the seed before rendering.
    private async Task ResumeAsync(FakeComponentStateStore seed)
    {
        await Services.GetRequiredService<ComponentStatePersistenceManager>().RestoreStateAsync(seed);
        Render<CounterView>();
    }

    private void Configure(params Action<PersistOptions>[] persist)
    {
        Services.AddSingleton(_log);
        Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs)));
        AddDucky(this, o => Array.ForEach(persist, configure => configure(o)), d => d.Use<HydrationRecorder>());
    }

    // No debounce: a change is written at once. Time never advances, so a failed write waits in its backoff until dispose.
    private void AddDucky(BunitContext context, Action<PersistOptions> persist, Action<DuckyBuilder>? more = null)
    {
        var user = _user;
        context.Services.AddSingleton<TimeProvider>(_time);
        context.Services.RemoveAll<IJSRuntime>();
        context.Services.AddSingleton<IJSRuntime>(_js);
        context.Services.AddSingleton(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        context.Services.AddSingleton(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        context.Services.AddDucky(d =>
        {
            d.UseJson(HydrationJson.Default).AddSlice<CounterSlice>().AddBlazor(o => o.Scope = user is null ? null : (_, _) => new(user)).Prerender<CounterSlice>().Persist<CounterSlice>(o =>
            {
                o.Debounce = TimeSpan.Zero;
                persist(o);
            });
            more?.Invoke(d);
        });
        context.Renderer.SetRendererInfo(_server);
    }
}
