using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Bunit;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
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
    private readonly FakeLogCollector _pauseLogs = new(); // the paused circuit's
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
    private bool _disconnected;
    private bool _readHangs;
    private string? _user;
    private string _prefix = "ducky";
    private bool _anonymous; // a Scope that resolves null
    private SwitchingAuth? _auth; // the latest store's provider: a user change notifies it
    private TaskCompletionSource<string?>? _scopeGate; // when set, the next scope resolutions wait for it
    private TaskCompletionSource<object?>? _readGate; // when set, every storage read waits for it

    public PauseSeedTests() => _js.Respond = (identifier, args) => identifier switch
    {
        "import" => _js,
        "storageGet" => _readGate?.Task ?? (_readHangs ? new TaskCompletionSource<object?>().Task : _storage.GetValueOrDefault(((string)args[0]!, (string)args[1]!))),
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
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2017); // no scoped key, so nothing handed over to drop
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
    [InlineData("""{"v":1,"src":"pause","dirty":[null],"ver":{"counter":1},"slices":{"counter":{"Value":4}}}""", "restore:counter Hydrated:0", 0)]
    [InlineData("""{"v":1,"src":"pause","dirty":["counter"],"ver":{"counter":1},"slices":{"counter":{"Value":"four"}}}""", "restore: Hydrated:0", 1)]
    [InlineData("""{"v":1,"src":"prerender","dirty":["counter"],"ver":{"counter":1},"slices":{"counter":{"Value":4}}}""", "restore:counter Hydrated:0", 0)]
    public async Task PauseSeed_UnusableDirtyEntry_StorageRestoredAndNotOverwritten(string seed, string seedRestore, int undeserializable)
    {
        // (non-normative) A null dirty entry is no key (the seed is still restored), a prerender seed's key is never dirty
        // (the override is a pause rule, §11.4), and a dirty value the store can't
        // restore (a state type changed without a Version bump) is not a value the seed restored (§11.5 step 6): storage
        // applies, and nothing overwrites it. That value is left out of the seed restore, so its Warning 1071 is logged once.
        _storage[("local", Key)] = Envelope(3, version: 1);

        Configure();
        await ResumeAsync(Forged(seed));
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(3));
        _log.Entries[0].ShouldBe(seedRestore);
        _logs.GetSnapshot().Count(static r => r.Id.Id == 1071).ShouldBe(undeserializable);
        await AssertNothingWrittenAsync(Envelope(3, version: 1));
    }

    [Theory]
    [InlineData("bob", true, 9)]
    [InlineData("bob", false, 0)]
    [InlineData(null, false, 0)]
    public async Task Circuit_ResumeUnderOtherUser_PauseSeedScopedSlicesNotRestoredOrWritten(string? resumedAs, bool stored, int expected)
    {
        // Alice's circuit paused with a dirty 4; during the disconnect she signed out, or Bob signed in on another tab, and the
        // circuit resumes under that user. The seed records only the hash of Alice's scope; its scoped slice is never restored
        // synchronously, and step 6 drops it (Debug 2017) since the resumed scope does not hash to it: Bob's storage, or the
        // initial state, applies, and Alice's value is never written under Bob's key (INV-17).
        _storage[("local", "ducky:alice:counter")] = Envelope(3, version: 1);
        if (stored)
        {
            _storage[("local", "ducky:bob:counter")] = Envelope(9, version: 1);
        }

        _user = "alice";
        var seed = await PauseAsync(increments: 1, disconnected: true);
        var envelope = Seed(seed);
        envelope["dirty"]!.AsArray().Count.ShouldBe(1);
        envelope["scope"]!.GetValue<string>().ShouldBe(Sha256("alice"));
        envelope.ToJsonString().ShouldNotContain("alice");

        (_user, _anonymous) = (resumedAs, resumedAs is null);
        Configure();
        await ResumeAsync(seed);
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(expected));
        _log.Entries[0].ShouldBe("restore: Hydrated:0");
        var dropped = _logs.GetSnapshot().Where(static r => r.Id.Id == 2017).ShouldHaveSingleItem();
        dropped.Level.ShouldBe(LogLevel.Debug);
        dropped.Message.ShouldContain("counter");
        await Store.DisposeAsync();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
        _storage.GetValueOrDefault(("local", "ducky:bob:counter")).ShouldBe(stored ? Envelope(9, version: 1) : null);
        _storage[("local", "ducky:alice:counter")].ShouldBe(Envelope(3, version: 1));
    }

    [Fact]
    public async Task PauseSeed_ResumeUnderSameUser_DirtyScopedKeyWinsAndIsRewritten()
    {
        // (non-normative, §11.5 step 6) Resumed under Alice again: her scope hashes to the seed's, so step 6 restores the
        // seed's dirty 4 in place of the stale stored 3 (baseline from the read) and rewrites her key after the terminal.
        _storage[("local", "ducky:alice:counter")] = Envelope(3, version: 1);
        _user = "alice";
        var seed = await PauseAsync(increments: 1, disconnected: true);

        Configure();
        await ResumeAsync(seed);
        await Store.InitializeAsync(Ct);

        Payload(await NextWriteAsync()).ShouldBe("""{"Value":4}""");
        Payload((string)_storage[("local", "ducky:alice:counter")]!).ShouldBe("""{"Value":4}""");
        Store.State.Get<Counter>().ShouldBe(new Counter(4));
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        _log.Entries.ShouldBe([
            "restore: Hydrated:0",
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);

        // Taken once: a later switch has no seed left to drop.
        SwitchTo("bob");
        await WriterHarness.Until(() => Store.State.Get<PersistenceState>() is { Status: PersistenceStatus.Hydrated, ScopeEpoch: 1 });
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2017);
    }

    [Theory]
    [InlineData(PersistStorage.Local, false, true, 9)]
    [InlineData(PersistStorage.Local, false, false, 4)]
    [InlineData(PersistStorage.Server, true, true, 9)]
    [InlineData(PersistStorage.Server, true, false, 4)]
    public async Task PauseSeed_ResumeUnderSameUser_CleanOrServerScopedKey_StorageWinsElseSeedValue(PersistStorage storage, bool listedDirty, bool stored, int expected)
    {
        // (non-normative, §11.4) Under the matching scope a clean browser key, and a Server key even when listed as dirty (the
        // override never applies to Server storage), take the seed's value only when storage holds no valid envelope.
        (_user, _prefix) = ("alice", "app");
        var seed = AliceSeed("""{"Value":4}""", listedDirty);
        Configure(o => o.Storage = storage);
        if (stored)
        {
            await StoreAsync(storage, "app:alice:counter", Envelope(9, version: 1));
        }

        await ResumeAsync(seed);
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(expected));
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        await Store.DisposeAsync();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
        if (storage == PersistStorage.Server)
        {
            (await Services.GetRequiredService<IDistributedCache>().GetStringAsync("app:alice:counter", Ct)).ShouldBe(stored ? Envelope(9, version: 1) : null);
        }
    }

    [Theory]
    [InlineData("null", 0)]
    [InlineData("""{"Value":"four"}""", 1)]
    public async Task PauseSeed_UnusableScopedEntry_NotHandedOver(string value, int undeserializable)
    {
        // (non-normative) A scoped value the store can't restore (null, or a state type changed without a Version bump) is no
        // value to hand over: under the matching scope with nothing stored the initial state stays, and nothing is written.
        _user = "alice";
        var seed = AliceSeed(value, dirty: true);
        Configure();
        await ResumeAsync(seed);
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(0));
        Store.State.WasRestored<Counter>().ShouldBeFalse();
        _logs.GetSnapshot().Count(static r => r.Id.Id == 1071).ShouldBe(undeserializable);
        await Store.DisposeAsync();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task PauseSeed_NoScopeRecorded_ScopedKeyDroppedEvenUnderNullScope()
    {
        // (non-normative) A pause seed that carries a scoped key but records no scope (forged, or edited) matches no scope, a
        // null one included: dropped (Debug 2017), and the initial state stays.
        _anonymous = true;
        Configure();
        await ResumeAsync(Forged("""{"v":1,"src":"pause","dirty":["counter"],"ver":{"counter":1},"slices":{"counter":{"Value":4}}}"""));
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(0));
        _logs.GetSnapshot().Count(static r => r.Id.Id == 2017).ShouldBe(1);
        await Store.DisposeAsync();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task PauseSeed_SwitchSupersedesFirstAttempt_ScopedSeedDropped()
    {
        // (non-normative, §11.5 step 6) Alice is re-notified while the resumed store's first scope resolution is still
        // pending: the switch supersedes attempt 0 before it records a scope, so no scope of epoch 0 hashes to the seed's,
        // and the switch attempt's terminal drops it (Debug 2017): her stored 3 applies.
        _storage[("local", "ducky:alice:counter")] = Envelope(3, version: 1);
        _user = "alice";
        var seed = await PauseAsync(increments: 1, disconnected: true);

        var scope = _scopeGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Configure();
        await ResumeAsync(seed);
        _scopeGate = null;
        _auth!.Change("alice");
        scope.SetResult("alice");
        await Store.InitializeAsync(Ct);
        await WriterHarness.Until(() => Store.State.Get<PersistenceState>() is { Status: PersistenceStatus.Hydrated, ScopeEpoch: 1 });

        Store.State.Get<Counter>().ShouldBe(new Counter(3));
        _logs.GetSnapshot().Count(static r => r.Id.Id == 2017).ShouldBe(1);
        await AssertNothingWrittenAsync("ducky:alice:counter", Envelope(3, version: 1));
    }

    [Fact]
    public async Task PauseSeed_SwitchAttemptUnderRecordedMatchingScope_ScopedSeedDropped()
    {
        // (non-normative, §11.5 step 6, INV-17) Epoch 0 resolved to Alice, whose scope hashes to the seed's, but her read hangs
        // and Bob signs in: only attempt 0 may apply the seed, so the switch attempt drops it (Debug 2017), Bob's store never
        // renders Alice's 4, and nothing is written under Bob's key.
        _user = "alice";
        var seed = AliceSeed("""{"Value":4}""", dirty: true);
        _readHangs = true;
        Configure();
        await ResumeAsync(seed);
        await WriterHarness.Until(() => _js.Calls.Any(static call => call.Identifier == "storageGet"));

        _readHangs = false;
        SwitchTo("bob");
        await Store.InitializeAsync(Ct);
        await WriterHarness.Until(() => Store.State.Get<PersistenceState>() is { Status: PersistenceStatus.Hydrated, ScopeEpoch: 1 });

        Store.State.Get<Counter>().ShouldBe(new Counter(0));
        _logs.GetSnapshot().Count(static r => r.Id.Id == 2017).ShouldBe(1);
        await Store.DisposeAsync();
        _storage.ContainsKey(("local", "ducky:bob:counter")).ShouldBeFalse();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task PauseSeed_SupersededFirstAttemptReadsFirst_ScopedSeedDroppedOnce()
    {
        // (non-normative, §11.5 step 6) Epoch 0 resolved to Alice and Bob signs in while her read is in flight; that read then
        // completes before the switch attempt's. Attempt 0 is no longer current, so it never applies the seed: the seed is
        // dropped once (Debug 2017), Bob's stored 9 applies, and nothing is written.
        _storage[("local", "ducky:bob:counter")] = Envelope(9, version: 1);
        _user = "alice";
        var seed = AliceSeed("""{"Value":4}""", dirty: true);
        var first = _readGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Configure();
        await ResumeAsync(seed);
        await WriterHarness.Until(() => _js.Calls.Any(static call => call.Identifier == "storageGet"));

        var second = _readGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        SwitchTo("bob");
        first.SetResult(Envelope(3, version: 1));
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == 2041));
        second.SetResult(Envelope(9, version: 1));
        await Store.InitializeAsync(Ct);
        await WriterHarness.Until(() => Store.State.Get<PersistenceState>() is { Status: PersistenceStatus.Hydrated, ScopeEpoch: 1 });

        Store.State.Get<Counter>().ShouldBe(new Counter(9));
        _logs.GetSnapshot().Count(static r => r.Id.Id == 2017).ShouldBe(1);
        await AssertNothingWrittenAsync("ducky:bob:counter", Envelope(9, version: 1));
    }

    [Fact]
    public async Task PauseSeed_CrossTabValueDuringHydration_WinsOverScopedSeed()
    {
        // (non-normative, §11.5 step 6) Another tab writes Alice's key while the resumed store's read is in flight: that value
        // is newer than the seed's dirty 4, so it stays, and the seed never rolls it back.
        _user = "alice";
        var seed = AliceSeed("""{"Value":4}""", dirty: true);
        var read = _readGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Configure(static o => o.SyncAcrossTabs = true);
        await ResumeAsync(seed);
        await WriterHarness.Until(() => _js.Calls.Any(static call => call.Identifier == "storageGet"));

        var sync = (DotNetObjectReference<PersistenceMiddleware.CrossTabSync>)_js.Calls.Single(static call => call.Identifier == "watchStorage").Args[1]!;
        var handled = sync.Value.OnStorageChangedAsync("tab", "local", "ducky:alice:counter", Envelope(7, version: 1), tooLarge: false);
        _time.Advance(TimeSpan.FromMilliseconds(50));
        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        read.SetResult(Envelope(3, version: 1));
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(7));
        await Store.DisposeAsync();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Circuit_PauseDuringScopeSwitch_StorageNotOverwrittenWithInitialState()
    {
        // Bob's circuit read his 9, then Alice signed in (3) and made it 4. While .NET pauses the circuit, Bob signs in again:
        // the switch lands between the idle wait and the serialization (AfterSeedIdleHook), so the snapshot is Hydrating for
        // Bob, its counter the reset initial state, which differs from Bob's last known 9. The seed leaves out every key of
        // that attempt (Debug 2016), so the resumed circuit restores Bob's 9 and never overwrites it with the initial state.
        _storage[("local", "ducky:bob:counter")] = Envelope(9, version: 1);
        _storage[("local", "ducky:alice:counter")] = Envelope(3, version: 1);
        _user = "bob";
        var seed = await PauseAsync(
            increments: 1,
            disconnected: false,
            before: async store =>
            {
                SwitchTo("alice");
                await WriterHarness.Until(() => store.State.Get<PersistenceState>() is { Status: PersistenceStatus.Hydrated, ScopeEpoch: 1 });
            },
            afterSeedIdle: () =>
            {
                _readHangs = true;
                SwitchTo("bob");
            });
        var envelope = Seed(seed);
        envelope["slices"]!["counter"].ShouldBeNull();
        envelope["dirty"].ShouldBeNull();
        var leftOut = _pauseLogs.GetSnapshot().Where(static r => r.Id.Id == 2016).ShouldHaveSingleItem();
        leftOut.Level.ShouldBe(LogLevel.Debug);
        leftOut.Message.ShouldContain("'counter'");

        _readHangs = false;
        Configure();
        await ResumeAsync(seed);
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(9));
        await AssertNothingWrittenAsync("ducky:bob:counter", Envelope(9, version: 1));
    }

    [Fact]
    public async Task PauseSeed_ServerStorageKey_NeverDirty()
    {
        // (non-normative) The pause override never applies to Server storage (§11.4): its key is versioned, never dirty.
        (_user, _prefix) = ("alice", "app");
        var seed = await PauseAsync(increments: 1, disconnected: true, static o => o.Storage = PersistStorage.Server);

        var envelope = Seed(seed);
        envelope["slices"]!["counter"].ShouldNotBeNull();
        envelope["ver"]!["counter"]!.GetValue<int>().ShouldBe(1);
        envelope["dirty"].ShouldBeNull();
    }

    [Fact]
    public async Task PauseSeed_NullScope_ScopedKeyLeftOut()
    {
        // (non-normative) §11.4, §11.6: under a null scope (an anonymous user, with a Scope set) a scoped key names no stored
        // key, so the pause leaves it out (Debug 2016) and records no scope.
        _anonymous = true;
        var envelope = Seed(await PauseAsync(increments: 1, disconnected: true));
        envelope["slices"]!["counter"].ShouldBeNull();
        envelope.AsObject().ContainsKey("scope").ShouldBeFalse();
        _pauseLogs.GetSnapshot().Count(static r => r.Id.Id == 2016).ShouldBe(1);
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

    [Fact]
    public async Task PauseSeed_ScopedKey_HydrationTimesOut_DroppedOnce()
    {
        // (non-normative, §11.4) Resumed under Alice, but the read hangs past HydrationTimeout: step 6 never runs, so the
        // scoped seed is not applied and is dropped at the failed terminal (Debug 2017), not kept for a later attempt.
        _user = "alice";
        var seed = AliceSeed("""{"Value":4}""", dirty: true);
        Configure();
        _readHangs = true;
        await ResumeAsync(seed);
        var init = Store.InitializeAsync(Ct);
        _time.Advance(TimeSpan.FromSeconds(5));
        await init;

        _log.Entries.ShouldContain("failed:TimeoutException:0:System Failed:0");
        Store.State.Get<Counter>().ShouldBe(new Counter(0));
        _logs.GetSnapshot().Count(static r => r.Id.Id == 2017).ShouldBe(1);

        _readHangs = false;
        SwitchTo("bob");
        await WriterHarness.Until(() => Store.State.Get<PersistenceState>() is { Status: PersistenceStatus.Hydrated, ScopeEpoch: 1 });
        _logs.GetSnapshot().Count(static r => r.Id.Id == 2017).ShouldBe(1);
        await Store.DisposeAsync();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
    }

    private static FakeComponentStateStore Forged(string envelope)
    {
        var seed = new FakeComponentStateStore();
        seed.State[PrerenderHandoff.SeedKey] = JsonSerializer.SerializeToUtf8Bytes(Encoding.UTF8.GetBytes(envelope));
        return seed;
    }

    private Task AssertNothingWrittenAsync(string stored) => AssertNothingWrittenAsync(Key, stored);

    // Writer loops run outside quiescence: dispose flushes every writer and released deferred key (phase 5a) first.
    private async Task AssertNothingWrittenAsync(string key, string stored)
    {
        await Store.DisposeAsync();
        _writes.Reader.TryRead(out _).ShouldBeFalse();
        _storage[("local", key)].ShouldBe(stored);
    }

    // A pause seed captured under Alice's scope, carrying the counter's value, listed as dirty or not.
    private static FakeComponentStateStore AliceSeed(string value, bool dirty) => Forged(
        $$"""{"v":1,"src":"pause","scope":"{{Sha256("alice")}}",{{(dirty ? "\"dirty\":[\"counter\"]," : "")}}"ver":{"counter":1},"slices":{"counter":""" + value + "}}");

    private static string Sha256(string scope) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));

    // The signed-in user changes the way a circuit does: the scope resolves to the new user, and the provider notifies.
    private void SwitchTo(string user)
    {
        _user = user;
        _auth!.Change(user);
    }

    private async Task StoreAsync(PersistStorage storage, string key, string envelope)
    {
        if (storage == PersistStorage.Server)
        {
            await Services.GetRequiredService<IDistributedCache>().SetAsync(key, Encoding.UTF8.GetBytes(envelope), Ct);
        }
        else
        {
            _storage[("local", key)] = envelope;
        }
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
    // `before` runs once the circuit hydrated, before the increments; `afterSeedIdle` is the AfterSeedIdleHook of the pause.
    private async Task<FakeComponentStateStore> PauseAsync(int increments, bool disconnected, Action<PersistOptions>? persist = null, Func<IStore, Task>? before = null, Action? afterSeedIdle = null)
    {
        _disconnected = disconnected;
        var persisted = new FakeComponentStateStore();
        await using (var circuit = new BunitContext())
        {
            circuit.Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_pauseLogs)));
            AddDucky(circuit, persist ?? (static _ => { }), afterSeedIdle: afterSeedIdle);
            circuit.Render<CounterView>();
            var store = circuit.Services.GetRequiredService<IStore>();
            await store.InitializeAsync(Ct);
            if (before is not null)
            {
                await before(store);
            }

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
    // The Scope resolves to the current _user when it resolves, unless _scopeGate holds it; a store with a user gets a
    // SwitchingAuth, so a user change is a scope switch.
    private void AddDucky(BunitContext context, Action<PersistOptions> persist, Action<DuckyBuilder>? more = null, Action? afterSeedIdle = null)
    {
        var (user, prefix, anonymous) = (_user, _prefix, _anonymous);
        Func<IServiceProvider, CancellationToken, ValueTask<string?>>? scope = anonymous
            ? static (_, _) => new((string?)null)
            : user is null ? null : (_, _) => _scopeGate is { } gate ? new(gate.Task) : new(_user);
        if (user is not null)
        {
            _auth = new SwitchingAuth(user);
            context.Services.AddSingleton<AuthenticationStateProvider>(_auth);
        }

        context.Services.AddSingleton<TimeProvider>(_time);
        context.Services.AddDistributedMemoryCache();
        context.Services.RemoveAll<IJSRuntime>();
        context.Services.AddSingleton<IJSRuntime>(_js);
        context.Services.AddSingleton(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        context.Services.AddSingleton(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        context.Services.AddDucky(d =>
        {
            d.UseJson(HydrationJson.Default).AddSlice<CounterSlice>().AddBlazor(o => (o.Scope, o.KeyPrefix, o.AfterSeedIdleHook) = (scope, prefix, afterSeedIdle)).Prerender<CounterSlice>().Persist<CounterSlice>(o =>
            {
                o.Debounce = TimeSpan.Zero;
                persist(o);
            });
            more?.Invoke(d);
        });
        context.Renderer.SetRendererInfo(_server);
    }
}
