using System.Text;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.DistributedCache;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.CrossTab;

// SPEC §11.7 (.NET CrossTabSync), INV-18, INV-23: one watchStorage registration per store before its first read, removed
// on dispose; OnStorageChanged accepts only this store's exact current keys (synced slices for "tab", area-matching
// browser-persisted slices for "doc"), debounces 50 ms per key, restores a removal as the initial state, unwraps a value
// with the hydration EnvelopeReader, drops an echo of the local baseline, and otherwise restores it under _issue (bumping
// the key's sequence number, so a hydration read that began earlier is dropped), sets the baseline and signals the writer.
public sealed class CrossTabTests
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task CrossTab_OriginNeverPersisted()
    {
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();

        // Two values inside one debounce: only the newest is restored, as a CrossTab restore that writes nothing. Its
        // baseline is the local serialization, and the writer it signalled dedupes against it.
        var first = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(4)), tooLarge: false);
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)));
        await first;
        x.H.Store.State.Get<Level>().ShouldBe(new(5));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Store.State.WasRestored<Level>().ShouldBeFalse();
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":5}""");
        await x.H.IterationsAsync("level", 1);
        x.H.Written.ShouldBeEmpty();

        // A removal restores the initial state, as a CrossTab restore too, and writes nothing either.
        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level"), (Origin.CrossTab, "level")]);
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Written.ShouldBeEmpty();

        // A local change is still written.
        x.H.Store.Dispatch(new SetLevel(6));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":6}""");
    }

    [Fact]
    public async Task CrossTab_EchoDropped()
    {
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);

        // The other store relays the same state back, in its own spelling: compared on the local re-serialization of s,
        // never on the text, it is an echo, and nothing is restored or written.
        await x.ReceiveAsync("doc", "local", "ducky:level", EnvelopeWriter.Write("""{ "Value" : 3 }""", 1, x.H.Time.GetUtcNow()));
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(3)));
        x.Restores.Entries.ShouldBeEmpty();
        x.H.Middleware.Writers["level"].Iterations.ShouldBe(1);
        x.H.Written.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CrossTab_TooLarge_ReReadViaStream()
    {
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        var pulls = new Queue<object?>();
        var respond = x.H.Js.Respond;
        x.H.Js.Respond = (identifier, args) => identifier == "storageGetStream" ? pulls.Dequeue() : respond(identifier, args);
        await x.H.InitializeAsync();

        // A too-large notification superseded within its debounce is never pulled: only the key's newest notification is
        // read (an extra pull would find the queue empty and fail with Warning 2051).
        var superseded = x.Notify("tab", "local", "ducky:level", null, tooLarge: true);
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(6)));
        await superseded;
        x.H.Store.State.Get<Level>().ShouldBe(new(6));
        x.H.Js.Calls.ShouldNotContain(static call => call.Identifier == "storageGetStream");
        x.EventIds(LogLevel.Warning).ShouldNotContain(2051);

        // ducky.js found the value above InlinePayloadBytes: .NET pulls that key by stream, and disposes the reference.
        var big = new FakeJsStreamReference(Encoding.UTF8.GetBytes(x.Envelope(new Level(7))));
        pulls.Enqueue(big);
        await x.ReceiveAsync("tab", "local", "ducky:level", null, tooLarge: true);
        x.H.Store.State.Get<Level>().ShouldBe(new(7));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level"), (Origin.CrossTab, "level")]);
        big.Disposed.ShouldBeTrue();
        x.H.Js.Calls.Where(static call => call.Identifier == "storageGetStream").ShouldHaveSingleItem().Args.ShouldBe(["local", "ducky:level"]);
        x.H.Js.Calls.Count(static call => call.Identifier == "storageGet").ShouldBe(2); // the hydration read and the value's re-read

        // A newer notification that lands while the pull is in flight wins: the late pull is dropped.
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        pulls.Enqueue(held.Task);
        var pulling = x.Notify("tab", "local", "ducky:level", null, tooLarge: true);
        x.H.Time.Advance(CrossTabHarness.Debounce);
        await WriterHarness.Until(() => pulls.Count == 0);
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(9)));
        var late = new FakeJsStreamReference(Encoding.UTF8.GetBytes(x.Envelope(new Level(8))));
        held.SetResult(late);
        await pulling;
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Store.State.Get<Level>().ShouldBe(new(9));
        late.Disposed.ShouldBeTrue();

        // A pull the circuit's disconnect interrupted is no value and no removal: ignored with a Warning, never thrown.
        pulls.Enqueue(Task.FromException<object?>(new JSDisconnectedException("circuit gone")));
        await x.ReceiveAsync("tab", "local", "ducky:level", null, tooLarge: true);
        x.H.Store.State.Get<Level>().ShouldBe(new(9));
        x.EventIds(LogLevel.Warning).ShouldContain(2051);
        x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2051).Message.ShouldContain("ducky:level");
    }

    [Fact]
    public async Task CrossTab_NonAsciiValueUnderCharLimitOverByteLimit_UsesStream()
    {
        // 12 Ki CJK characters: under InlinePayloadBytes in UTF-16 code units, about three times over it in UTF-8 bytes,
        // which is what ducky.js measures and what crosses the hub (§11.9). It arrives by stream and decodes intact.
        await using var x = new CrossTabHarness(static d => d.Persist<NoteSlice>(static o => o.SyncAcrossTabs = true));
        var text = new string('漢', 12 * 1024);
        var envelope = EnvelopeWriter.Write($$"""{"Text":"{{text}}"}""", 1, x.H.Time.GetUtcNow());
        envelope.Length.ShouldBeLessThan(16 * 1024 + 64);
        Encoding.UTF8.GetByteCount(envelope).ShouldBeGreaterThan(16 * 1024);
        var respond = x.H.Js.Respond;
        x.H.Js.Respond = (identifier, args) => identifier == "storageGetStream" ? new FakeJsStreamReference(Encoding.UTF8.GetBytes(envelope)) : respond(identifier, args);
        await x.H.InitializeAsync();

        await x.ReceiveAsync("tab", "local", "ducky:note", null, tooLarge: true);
        x.H.Store.State.Get<Note>().Text.ShouldBe(text);
    }

    [Fact]
    public async Task CrossTab_OtherScopeKey_Ignored()
    {
        // Scoped keys are matched against the scope recorded for the current epoch: another user's key, the unscoped form,
        // another prefix and a slice this store does not persist are all ignored with a Debug log.
        // A Blazor Server store: its scope delegate needs no renderer hand-off.
        await using var x = new CrossTabHarness(
            static d => d
                .AddBlazor(static o => o.Scope = static (_, _) => ValueTask.FromResult<string?>("alice"))
                .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true),
            browser: false);
        await x.H.InitializeAsync();
        foreach (var key in (string[])["ducky:bob:level", "ducky:level", "other:alice:level", "app:alice:dial", "ducky:alice:level:x"])
        {
            await x.ReceiveAsync("tab", "local", key, x.Envelope(new Level(1)));
        }

        x.Restores.Entries.ShouldBeEmpty();
        x.Logs.GetSnapshot().Where(static record => record.Id.Id == 2050).Select(static record => record.Level).ShouldBe(Enumerable.Repeat(LogLevel.Debug, 5));
        x.Logs.GetSnapshot().First(static record => record.Id.Id == 2050).Message.ShouldContain("ducky:bob:level");

        // The current scope's key is accepted.
        await x.ReceiveAsync("tab", "local", "ducky:alice:level", x.Envelope(new Level(2)));
        x.H.Store.State.Get<Level>().ShouldBe(new(2));

        // A null scope names no key: none is ever accepted.
        await using var anonymous = new CrossTabHarness(
            static d => d
                .AddBlazor(static o => o.Scope = static (_, _) => ValueTask.FromResult<string?>(null))
                .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true),
            browser: false);
        await anonymous.H.InitializeAsync();
        await anonymous.ReceiveAsync("tab", "local", "ducky:level", anonymous.Envelope(new Level(3)));
        await anonymous.ReceiveAsync("tab", "local", "ducky::level", anonymous.Envelope(new Level(3)));
        anonymous.Restores.Entries.ShouldBeEmpty();
        anonymous.EventIds(LogLevel.Debug).Count(static id => id == 2050).ShouldBe(2);
    }

    [Fact]
    public async Task CrossTab_AcceptedKeys_FollowSourceAndArea()
    {
        // "tab" (another tab's storage event) reaches synced slices only; "doc" (a relay from another store of this
        // document) reaches every browser-persisted slice whose area matches, synced or not.
        await using var x = new CrossTabHarness(static d => d
            .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true)
            .Persist<DialSlice>()
            .Persist<CounterSlice>(static o => o.Storage = PersistStorage.Session));
        await x.H.InitializeAsync();

        await x.ReceiveAsync("tab", "local", "ducky:dial", x.Envelope(new Dial(1)));
        await x.ReceiveAsync("tab", "session", "ducky:counter", x.Envelope(new Counter(1)));
        await x.ReceiveAsync("doc", "local", "ducky:counter", x.Envelope(new Counter(1)));
        await x.ReceiveAsync("doc", "session", "ducky:dial", x.Envelope(new Dial(1)));
        x.Restores.Entries.ShouldBeEmpty();
        x.EventIds(LogLevel.Debug).Count(static id => id == 2050).ShouldBe(4);

        await x.ReceiveAsync("doc", "local", "ducky:dial", x.Envelope(new Dial(2)));
        await x.ReceiveAsync("doc", "session", "ducky:counter", x.Envelope(new Counter(3)));
        await x.ReceiveAsync("doc", "local", "ducky:level", x.Envelope(new Level(4)));
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)));
        x.Restores.Entries.Select(static entry => entry.Keys).ShouldBe(["dial", "counter", "level", "level"]);
        (x.H.Store.State.Get<Dial>(), x.H.Store.State.Get<Counter>(), x.H.Store.State.Get<Level>()).ShouldBe((new(2), new(3), new(5)));
    }

    [Fact]
    public async Task CrossTab_NullKey_ReReadsEveryAcceptedKey()
    {
        // clear() from another script, or a resync after failed delivery: every accepted key of that source and area is read
        // again; a value takes the value branch, a missing one the removal branch.
        await using var x = new CrossTabHarness(static d => d
            .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true)
            .Persist<DialSlice>(static o => o.SyncAcrossTabs = true)
            .Persist<CounterSlice>());
        await x.H.InitializeAsync();
        // The dial's write has landed, so its baseline exists: its missing value is a removal (an unwritten one is kept).
        x.H.Store.Dispatch(new SetDial(5));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":5}""");
        await x.H.IterationsAsync("dial", 1);
        x.H.Store.Dispatch(new Increment());
        x.H.Storage[("local", "ducky:level")] = x.Envelope(new Level(6));

        await x.ReceiveAsync("tab", "local", null, null);
        x.H.Js.Calls.Where(static call => call.Identifier == "storageGet").Skip(3).Select(static call => (string)call.Args[1]!)
            .ShouldBe(["ducky:level", "ducky:dial"], ignoreOrder: true);
        x.H.Store.State.Get<Level>().ShouldBe(new(6));
        x.H.Store.State.Get<Dial>().ShouldBe(new(0));
        x.H.Store.State.Get<Counter>().ShouldBe(new(1));
    }

    [Fact]
    public async Task CrossTab_NewerVersionEnvelope_Ignored()
    {
        // D18: an envelope from a newer version of the app (a rollback) is not found, Warning 2031, never migrated forward.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();

        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5), version: 2));
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.Restores.Entries.ShouldBeEmpty();
        x.EventIds(LogLevel.Warning).ShouldContain(2031);
    }

    [Fact]
    public async Task CrossTab_CtorThrowingPayload_Ignored()
    {
        // §10: a payload whose state constructor throws is ignored with DuckyJson's Warning naming the key; the handler completes.
        // So is one this store can't serialize back (no baseline could guard it against an echo), and a foreign value.
        await using var x = new CrossTabHarness(static d => d
            .AddSlice<PickySlice>().Persist<PickySlice>(static o => o.SyncAcrossTabs = true)
            .AddSlice<UnwritableSlice>().Persist<UnwritableSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();

        await x.ReceiveAsync("tab", "local", "ducky:picky", EnvelopeWriter.Write("""{"Value":-1}""", 1, x.H.Time.GetUtcNow()));
        await x.ReceiveAsync("tab", "local", "ducky:unwritable", EnvelopeWriter.Write("""{"Value":1}""", 1, x.H.Time.GetUtcNow()));
        await x.ReceiveAsync("tab", "local", "ducky:picky", """{"x":1}""");
        x.Restores.Entries.ShouldBeEmpty();
        x.H.Store.State.Get<Picky>().Value.ShouldBe(0);
        x.Logs.GetSnapshot().Where(static record => record.Id.Id == 1071).ShouldHaveSingleItem().Message.ShouldContain("ducky:picky");
        x.EventIds(LogLevel.Debug).ShouldContain(2030);

        await x.ReceiveAsync("tab", "local", "ducky:picky", EnvelopeWriter.Write("""{"Value":2}""", 1, x.H.Time.GetUtcNow()));
        x.H.Store.State.Get<Picky>().Value.ShouldBe(2);
    }

    [Fact]
    public async Task CrossTab_EventDuringHydration_NewestWins()
    {
        // Tab B writes while tab A's read is in flight: B's value lands first, as a Hydration restore (the value comes from
        // storage, so WasRestored is true and the load on StoreInitialized does not run), and A's older read is dropped.
        await using var x = new CrossTabHarness(static d => d
            .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true)
            .AddEffect<LoadUnlessRestored>());
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.HoldNextRead(read);
        var init = x.H.InitializeAsync();
        await WriterHarness.Until(() => x.H.Js.Calls.Any(static call => call.Identifier == "storageGet"));

        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(2)));
        x.H.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Hydrating);
        x.H.Store.State.Get<Level>().ShouldBe(new(2));
        x.H.Store.State.WasRestored<Level>().ShouldBeTrue();

        read.SetResult(x.Envelope(new Level(1)));
        await init;
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Store.State.Get<Level>().ShouldBe(new(2));
        x.H.Store.State.WasRestored<Level>().ShouldBeTrue();
        x.Restores.Entries.ShouldBe([(Origin.Hydration, "level")]);
        x.Restores.Completed.ShouldBe([true]); // B's value was restored by this hydration, though A's read was dropped
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":2}""");

        // After the terminal a value is a CrossTab restore; the deferred key's writer dedupes against B's baseline.
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(3)));
        x.Restores.Entries.Last().ShouldBe((Origin.CrossTab, "level"));
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Written.ShouldBeEmpty();
    }

    [Fact]
    public async Task CrossTab_Registration_OnceBeforeTheReadAndRemovedOnDispose()
    {
        // One watchStorage per store, with the store id, its reference, the key prefix, whether a synced slice listens to
        // other tabs, the 10-minute prune cap and the inline budget; issued before the first read; unwatchStorage on dispose.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true).Persist<DialSlice>());
        await x.H.InitializeAsync();
        var watch = x.Watches.ShouldHaveSingleItem();
        var id = (string)watch[0]!;
        id.ShouldMatch("^[0-9a-f]{32}$");
        watch.Skip(2).ShouldBe(["ducky", true, 600_000L, 16 * 1024]);
        PersistenceMiddleware.CrossTabSync.DeliveryPruneAfter.ShouldBe(TimeSpan.FromMinutes(10));
        x.H.Js.Calls.Select(static call => call.Identifier).Where(static i => i is "watchStorage" or "storageGet").First().ShouldBe("watchStorage");

        // A notification still in its debounce at dispose is never restored; the reference ducky.js held is released.
        var sync = x.Sync;
        var pending = sync.OnStorageChangedAsync("tab", "local", "ducky:level", x.Envelope(new Level(1)), tooLarge: false);
        x.H.Store.Dispatch(new SetDial(1));
        (await x.H.NextWriteAsync()).Id.ShouldBe(id);
        await x.H.DisposeStoreAsync();
        await pending.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        x.Restores.Entries.ShouldBeEmpty();
        x.H.Js.Calls.Where(static call => call.Identifier == "unwatchStorage").ShouldHaveSingleItem().Args.ShouldBe([id]);
        Should.Throw<ObjectDisposedException>(() => x.Sync);

        // Without a synced slice the store still registers, for same-document relays only.
        await using var unsynced = new CrossTabHarness(static d => d.Persist<LevelSlice>());
        await unsynced.H.InitializeAsync();
        unsynced.Watches.ShouldHaveSingleItem()[3].ShouldBe(false);
    }

    [Fact]
    public async Task CrossTab_Dispose_UnwatchFails_StillReleased()
    {
        // A disposal never throws (§6.11): unwatchStorage rejected by ducky.js (a stale module without the export, a failed
        // re-import) is a Warning naming the store id, never the store's 1015; the reference ducky.js held and the module
        // are released all the same (the 10-minute prune cap bounds the id ducky.js keeps).
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        var failure = new JSException("unwatchStorage is not a function");
        var respond = x.H.Js.Respond;
        x.H.Js.Respond = (identifier, args) => identifier == "unwatchStorage" ? throw failure : respond(identifier, args);
        await x.H.InitializeAsync();
        var id = (string)x.Watches.ShouldHaveSingleItem()[0]!;

        await x.H.DisposeStoreAsync();

        x.H.Js.Calls.ShouldContain(static call => call.Identifier == "unwatchStorage");
        x.EventIds(LogLevel.Error).ShouldNotContain(1015);
        var warning = x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2053);
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(id);
        warning.Exception.ShouldBeSameAs(failure);
        Should.Throw<ObjectDisposedException>(() => x.Sync);
        x.H.Js.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task CrossTab_Watch_PassesFullArgumentList()
    {
        // ducky.js's watchStorage takes six arguments and, as this fake, throws a TypeError and registers nothing when the
        // sixth (maxInlineBytes) is not a non-negative integer (§11.9): the sixth is Options.InlinePayloadBytes.
        await using var x = new CrossTabHarness(static d => d
            .AddBlazor(static o => o.InlinePayloadBytes = 4096)
            .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        var respond = x.H.Js.Respond;
        x.H.Js.Respond = (identifier, args) => identifier == "watchStorage" && (args.Length != 6 || args[5] is not (int and >= 0))
            ? throw new JSException("TypeError: watchStorage: maxInlineBytes")
            : respond(identifier, args);
        await x.H.InitializeAsync();
        var watch = x.Watches.ShouldHaveSingleItem();
        watch.Length.ShouldBe(6);
        watch.Skip(2).ShouldBe(["ducky", true, 600_000L, 4096]);
        await x.H.Store.WhenIdleAsync(Ct);
        x.EventIds(LogLevel.Warning).ShouldNotContain(2052);

        // The call's outcome is observed, never discarded: a rejected registration is a Warning naming the store id, and
        // the store still hydrates and writes.
        await using var rejected = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        var inner = rejected.H.Js.Respond;
        rejected.H.Js.Respond = (identifier, args) => identifier == "watchStorage" ? throw new JSException("TypeError: watchStorage: maxInlineBytes") : inner(identifier, args);
        rejected.H.Storage[("local", "ducky:level")] = rejected.Envelope(new Level(4));
        await rejected.H.InitializeAsync();
        rejected.H.Store.State.Get<Level>().ShouldBe(new(4));
        var id = (string)rejected.Watches.ShouldHaveSingleItem()[0]!;
        await WriterHarness.Until(() => rejected.EventIds(LogLevel.Warning).Contains(2052));
        var record = rejected.Logs.GetSnapshot().Single(static record => record.Id.Id == 2052);
        record.Message.ShouldContain(id);
        record.Exception.ShouldBeOfType<JSException>();
        rejected.H.Store.Dispatch(new SetLevel(5));
        (await rejected.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":5}""");

        // Any other fault of the call is observed too, never left to an unobserved task.
        await using var faulted = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        var fault = new ObjectDisposedException("module");
        var plain = faulted.H.Js.Respond;
        faulted.H.Js.Respond = (identifier, args) => identifier == "watchStorage" ? throw fault : plain(identifier, args);
        await faulted.H.InitializeAsync();
        await WriterHarness.Until(() => faulted.EventIds(LogLevel.Warning).Contains(2052));
        faulted.Logs.GetSnapshot().Single(static record => record.Id.Id == 2052).Exception.ShouldBeSameAs(fault);
    }

    [Theory]
    [InlineData("over", "key")]
    [InlineData("over", "all")]
    [InlineData("rejected", "key")]
    [InlineData("rejected", "all")]
    public async Task CrossTab_TooLarge_UnreadablePull_Ignored(string pull, string notification)
    {
        // §11.7: only a pull of Length <= 1 takes the removal branch. A stream re-read above MaxPayloadBytes is not found
        // with Warning 2023, and one the runtime rejected (S-5) with Warning 2026: the value is still stored, so the slice
        // keeps its state and nothing is restored, whether the notification named the key or re-reads every key.
        const int MaxPayloadBytes = 64 * 1024;
        await using var x = new CrossTabHarness(static d => d
            .AddBlazor(static o => o.MaxPayloadBytes = MaxPayloadBytes)
            .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        var respond = x.H.Js.Respond;
        x.H.Js.Respond = (identifier, args) => identifier != "storageGetStream" ? respond(identifier, args) : pull == "over"
            ? new FakeJsStreamReference(new byte[MaxPayloadBytes + 1])
            : Task.FromException<object?>(new JSException("Length must be a positive value"));
        await x.H.InitializeAsync();
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(9)));
        x.H.Storage[("local", "ducky:level")] = BrowserStorageProvider.TooLarge;

        await (notification == "key"
            ? x.ReceiveAsync("tab", "local", "ducky:level", null, tooLarge: true)
            : x.ReceiveAsync("tab", "local", null, null));

        x.H.Store.State.Get<Level>().ShouldBe(new(9));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Js.Calls.Count(static call => call.Identifier == "storageGetStream").ShouldBe(1);
        x.EventIds(LogLevel.Warning).ShouldBe([pull == "over" ? 2023 : 2026]);
        x.EventIds(LogLevel.Debug).ShouldNotContain(2030); // the Unreadable sentinel never reaches the EnvelopeReader
    }

    [Fact]
    public async Task CrossTab_ReceiptDuringScopeWait_LaterReadStands()
    {
        // §11.5 step 6 drops a read only for a cross-tab value received since that read began. In a Blazor Server store with
        // a Server slice (always scoped) the reads wait for the scope while the unscoped Local key is already accepted: a
        // value received during that wait moves the key's sequence number before the read begins, so the read, which may
        // hold a newer value (a later notification lost to a disconnect), still restores.
        var auth = new HeldAuthenticationStateProvider();
        await using var x = new CrossTabHarness(
            static d => d
                .AddBlazor(static o => o.KeyPrefix = "app")
                .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true).Persist<DialSlice>(static o => o.Storage = PersistStorage.Server),
            browser: false,
            services: s => s.AddSingleton<IDistributedCache>(new TestCache(yield: false)).AddSingleton<AuthenticationStateProvider>(auth));
        var init = x.H.InitializeAsync();
        await WriterHarness.Until(() => x.Watches.Count == 1);

        await x.ReceiveAsync("tab", "local", "app:level", x.Envelope(new Level(2)));
        x.H.Store.State.Get<Level>().ShouldBe(new(2));
        x.H.Js.Calls.Count(static call => call.Identifier == "storageGet").ShouldBe(1); // the receipt's own read, §11.7
        x.H.Storage[("local", "app:level")] = x.Envelope(new Level(3));

        auth.User.SetResult("alice");
        await init;
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Store.State.Get<Level>().ShouldBe(new(3));
        x.Restores.Entries.ShouldBe([(Origin.Hydration, "level"), (Origin.Hydration, "level")]);
        x.Restores.Completed.ShouldBe([true]);
        x.H.Middleware.LastKnownPayload["app:level"].ShouldBe("""{"Value":3}""");
    }

    [Fact]
    public async Task CrossTab_Dispose_ReceiptsStoppedBeforeTheFlush()
    {
        // Disposal stops receipts before the flush: a read still under way when it begins (one key) is never restored, and
        // a read its stop interrupted (another key) is no failure (no Warning 2051).
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true).Persist<DialSlice>());
        var opening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interrupted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pulls = new Queue<object?>();
        var respond = x.H.Js.Respond;
        x.H.Js.Respond = (identifier, args) => identifier == "storageGetStream" ? pulls.Dequeue() : respond(identifier, args);
        await x.H.InitializeAsync();
        var late = new FakeJsStreamReference(Encoding.UTF8.GetBytes(x.Envelope(new Level(7)))) { Opening = opening.Task };
        pulls.Enqueue(late);
        pulls.Enqueue(interrupted.Task);
        var sync = x.Sync;

        var reading = sync.OnStorageChangedAsync("tab", "local", "ducky:level", null, tooLarge: true);
        x.H.Time.Advance(CrossTabHarness.Debounce);
        await WriterHarness.Until(() => late.Opened);
        var pulling = sync.OnStorageChangedAsync("doc", "local", "ducky:dial", null, tooLarge: true);
        x.H.Time.Advance(CrossTabHarness.Debounce);
        await WriterHarness.Until(() => pulls.Count == 0);

        await x.H.DisposeStoreAsync();
        opening.SetResult();
        await Task.WhenAll(reading, pulling).WaitAsync(TimeSpan.FromSeconds(10), Ct);
        late.Disposed.ShouldBeTrue();
        x.Restores.Entries.ShouldBeEmpty();
        x.H.Middleware.LastKnownPayload.ShouldBeEmpty();
        x.EventIds(LogLevel.Warning).ShouldBeEmpty();
    }

    [Fact]
    public async Task CrossTab_Dispose_ReimportDuringFlush_RegistersNothing()
    {
        // The registration was lost to a failed import; the dispose flush imports afresh and writes, but re-registers
        // nothing: the id is about to be removed (unwatchStorage follows the flush) and its reference disposed.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => (o.SyncAcrossTabs, o.Debounce) = (true, TimeSpan.FromMinutes(1))), browser: false);
        var probe = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var respond = x.H.Js.Respond;
        var imports = 0;
        x.H.Js.Respond = (identifier, args) => identifier != "import" ? respond(identifier, args) : ++imports == 1 ? probe.Task : x.H.Js;
        var init = x.H.InitializeAsync();
        probe.SetException(new JSDisconnectedException("circuit gone"));
        await init;
        x.H.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Failed);
        x.H.Store.Dispatch(new SetLevel(1));
        await x.H.Time.TimerAsync(TimeSpan.FromMinutes(1));

        await x.H.DisposeStoreAsync();
        x.H.Written.ShouldHaveSingleItem().Payload.ShouldBe("""{"Value":1}""");
        imports.ShouldBe(2);
        x.H.Js.Calls.Select(static call => call.Identifier).ShouldBe(["import", "import", "storageSet", "unwatchStorage"]);
    }

    [Fact]
    public async Task CrossTab_ServerStorage_NeverWatchedNorAccepted()
    {
        // §11.7 registers a store that persists a Local or Session slice: Server storage (the distributed cache, §11.6)
        // is no browser storage, so a store holding only Server slices never calls watchStorage, and in a mixed store no
        // notification names a Server slice, whatever its area.
        static void Alice(DuckyBuilder d) => d.AddBlazor(static o => (o.KeyPrefix, o.Scope) = ("app", static (_, _) => ValueTask.FromResult<string?>("alice")));
        static void Cache(IServiceCollection s) => s.AddSingleton<IDistributedCache>(new TestCache(yield: false));
        await using var server = new CrossTabHarness(
            static d =>
            {
                Alice(d);
                d.Persist<DialSlice>(static o => o.Storage = PersistStorage.Server);
            },
            browser: false,
            services: Cache);
        await server.H.InitializeAsync();
        server.H.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Hydrated);
        server.Watches.ShouldBeEmpty();

        await using var mixed = new CrossTabHarness(
            static d =>
            {
                Alice(d);
                d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true).Persist<DialSlice>(static o => o.Storage = PersistStorage.Server);
            },
            browser: false,
            services: Cache);
        await mixed.H.InitializeAsync();
        await mixed.ReceiveAsync("doc", "session", "app:alice:dial", mixed.Envelope(new Dial(1)));
        await mixed.ReceiveAsync("doc", "local", "app:alice:dial", mixed.Envelope(new Dial(1)));
        mixed.Restores.Entries.ShouldBeEmpty();
        mixed.EventIds(LogLevel.Debug).Count(static id => id == 2050).ShouldBe(2);
        await mixed.ReceiveAsync("doc", "local", "app:alice:level", mixed.Envelope(new Level(2)));
        mixed.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
    }

    [Fact]
    public async Task CrossTab_NotificationForOtherRegistrationId_Rejected()
    {
        // §11.7, INV-18: a resumed circuit's stale registration can hold the DotNetObject id the new circuit gave this store's
        // reference, so its deliveries land here. Each names the registration id ducky.js holds it under: one that is not
        // this store's id is rejected and reads nothing, so the module prunes that registration at its cap; this store's
        // own notifications are still handled.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        var reads = x.H.Js.Calls.Count;

        var stale = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(4)), id: "0123456789abcdef0123456789abcdef");
        x.H.Time.Advance(CrossTabHarness.Debounce);

        (await Should.ThrowAsync<InvalidOperationException>(stale.WaitAsync(TimeSpan.FromSeconds(10), Ct))).Message.ShouldContain("0123456789abcdef0123456789abcdef");
        x.H.Js.Calls.Count.ShouldBe(reads);
        x.Restores.Entries.ShouldBeEmpty();
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)));
        x.H.Store.State.Get<Level>().ShouldBe(new(5));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
    }
}
