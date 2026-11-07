using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CsCheck;
using Ducky.Blazor.Tests.Persistence;
using Ducky.TestSupport;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Tests.CrossTab.Convergence;

// SPEC §11.5 "One baseline per storage key", §11.7, INV-16, INV-18: two tabs whose serializers emit the same set in
// different element orders (as string hashing seeded per process does) never echo, and remote changes that land while a
// local write is pending, or are applied after the receiver's own write landed, still leave both tabs and storage on one
// state, with no write triggered by a CrossTab receipt.
public sealed class CrossTabConvergenceTests
{
    // Kinds: 0/1 a local change in tab A/B, 2/3 completes A's/B's held write (the events already queued for that tab are
    // handed over first, though the storageGet of each runs after that write's setItem, sent before it; then setItem, then
    // the storage event in the other tab), 4/5 delivers A's/B's next storage event, 6/7 completes
    // A's/B's held write while that tab's queued events stay undelivered (they are applied after its own setItem: crossing
    // setItems, or a debounce that ends after it), 8/9 runs A's/B's held setItem (storage and the other tab's event) but
    // holds its result, so that tab's baseline lags what storage holds until a later step completes it, 10/11 hands A's/B's
    // next storage event over and runs its storageGet but holds the result, 12/13 answers it, so later steps (that tab's own
    // writes started and completed meanwhile) can overtake the value it read. Values are letter sets, as bits of 0-15.
    private static readonly Gen<(int Kind, int Bits)[]> _scripts = Gen.Select(Gen.Int[0, 13], Gen.Int[0, 15]).Array[0, 14];

    private static readonly ((int Kind, int Bits)[] Script, string[] Stored, (int A, int B) Writes)[] _fixed =
    [
        // Each tab receives the other's change while its own write is held: B's read of A's {a} runs after its own {b}, sent
        // first, and finds that echo; A takes {b}.
        ([(0, 0b0001), (1, 0b0010), (2, 0), (5, 0), (3, 0), (4, 0)], ["b"], (1, 1)),

        // A receipt while both tabs hold writes: B's read finds its own {b}, A's then its own {c}, which lands last. B takes
        // {c} while its {b} is in flight, and {b}'s success keeps that baseline: no rewrite.
        ([(0, 0b0001), (2, 0), (0, 0b0100), (1, 0b0010), (5, 0)], ["c"], (2, 1)),

        // Two receipts during one held write of B: both read its own {b}, which JS stored after A's {c}.
        ([(0, 0b0001), (2, 0), (0, 0b0100), (2, 0), (1, 0b0010), (5, 0), (5, 0)], ["b"], (2, 1)),

        // A receipt then a local change during one held write of B: the change ({d}) wins.
        ([(0, 0b0001), (2, 0), (1, 0b0010), (5, 0), (1, 0b1000)], ["d"], (1, 2)),

        // The bits a tab already holds are no change and never written again, in the writer and in a receiver.
        ([(0, 0b0001), (2, 0), (5, 0), (1, 0b0001), (0, 0b0001)], ["a"], (1, 0)),

        // Against an absent baseline the initial state is written once; with a write in flight, the same bits are not.
        ([(0, 0)], [], (1, 0)),
        ([(0, 0b0001), (0, 0b0001)], ["a"], (1, 0)),

        // Crossing setItems: B's {b} lands after A's {a}, and B applies A's stale event afterwards; it reads {b}, its own.
        ([(0, 0b0001), (1, 0b0010), (2, 0), (7, 0)], ["b"], (1, 1)),

        // B applies A's {a} only after its own {b} and A's newer {c} landed: it reads {c}, and writes nothing.
        ([(0, 0b0001), (1, 0b0010), (2, 0), (7, 0), (4, 0), (0, 0b0100), (2, 0)], ["c"], (2, 1)),

        // B's {b} landed, its result not back yet, and B changed to {d}: A's receipt reads B's own {b}, and {d} wins.
        ([(1, 0b0010), (0, 0b0001), (2, 0), (9, 0), (1, 0b1000), (5, 0)], ["d"], (1, 2)),

        // A completed write is no longer in flight: once A's {a} moved B's baseline, A's rewrite of B's earlier {b} applies.
        ([(1, 0b0010), (3, 0), (4, 0), (0, 0b0001), (2, 0), (5, 0), (0, 0b0010), (2, 0), (5, 0)], ["b"], (2, 1)),

        // B's read of A's {a} is answered only after B's own later {b} was written and acknowledged: the read is stale, and
        // dropped; started but not acknowledged yet, the same.
        ([(0, 0b0001), (2, 0), (11, 0), (1, 0b0010), (3, 0), (13, 0)], ["b"], (1, 1)),
        ([(0, 0b0001), (2, 0), (11, 0), (1, 0b0010), (13, 0)], ["b"], (1, 1)),

        // Two writes of B ({b}, then {c}) overtake one read: each started call counts.
        ([(0, 0b0001), (2, 0), (11, 0), (1, 0b0010), (3, 0), (1, 0b0100), (3, 0), (13, 0)], ["c"], (1, 2)),

        // B's read was issued after its held {b}, which JS runs first: the read finds B's own {b}, an echo.
        ([(0, 0b0001), (1, 0b0010), (2, 0), (11, 0), (3, 0), (13, 0)], ["b"], (1, 1)),

        // No write of B overtakes its read: the {a} it read is current, and restored.
        ([(1, 0b0010), (3, 0), (4, 0), (0, 0b0001), (2, 0), (11, 0), (13, 0)], ["a"], (1, 1)),

        // B's {} is in flight (landed, its result not back) when A writes {a} again, the value B's baseline still names: a
        // newer write, not an echo, so B takes it, and its {} succeeding keeps that baseline.
        ([(1, 0b0001), (3, 0), (4, 0), (1, 0), (9, 0), (4, 0), (0, 0b0001), (6, 0), (5, 0)], ["a"], (1, 2)),

        // B takes A's {a,c,d} while its {b,c,d} is in flight: storage no longer holds that write, so A's later {b,c,d},
        // equal to it, is no echo, and B takes it too.
        ([(0, 0b1101), (1, 0b1110), (9, 0), (4, 0), (0, 0b1110), (5, 0)], ["b", "c", "d"], (2, 1)),

        // A sampled counterexample: a write's success keeps the baseline a receipt set while it was in flight, so B, back
        // on the state it was writing, writes it again.
        ([(5, 2), (9, 2), (8, 14), (6, 14), (8, 4), (7, 15), (0, 13), (1, 0), (9, 15), (6, 13), (5, 11), (2, 13), (1, 0)], [], (1, 2)),
    ];

    [Fact]
    public async Task CrossTab_HashOrderedCollection_DifferentProcessOrder_Converges()
    {
        await using var tabs = await TabPair.StartAsync();
        var (a, b) = (tabs.A, tabs.B);

        // A writes {a,b,c} in its order; B reads it back in its own order, restores it, and its writer dedupes against
        // that local serialization, so nothing is written back.
        a.Change(0b0111);
        await tabs.SettleAsync();
        (await a.CompleteAsync()).ShouldBeTrue();
        tabs.Storage["ducky:tags"].ShouldContain("""["a","b","c"]""");
        (await b.DeliverAsync()).ShouldBeTrue();
        await tabs.SettleAsync();
        b.Items.ShouldBe(["a", "b", "c"], ignoreOrder: true);
        b.X.Restores.Entries.ShouldBe([(Origin.CrossTab, "tags")]);
        b.X.H.Middleware.LastKnownPayload["ducky:tags"].ShouldBe("""["c","b","a"]""");
        b.Writes.ShouldBe(0);

        // B changes the set: A takes it in its own order and writes nothing back either.
        b.Change(0b1110);
        await tabs.SettleAsync();
        (await b.CompleteAsync()).ShouldBeTrue();
        tabs.Storage["ducky:tags"].ShouldContain("""["d","c","b"]""");
        (await a.DeliverAsync()).ShouldBeTrue();
        await tabs.SettleAsync();
        a.Items.ShouldBe(["b", "c", "d"], ignoreOrder: true);
        a.X.H.Middleware.LastKnownPayload["ducky:tags"].ShouldBe("""["b","c","d"]""");

        // Quiet: at most one write per tab, each tab's own change.
        (await tabs.DrainAsync()).ShouldBe(0);
        a.Writes.ShouldBe(1);
        b.Writes.ShouldBe(1);
    }

    [Fact]
    public async Task CrossTab_CrossingWrites_ReceiptReadsCurrentValue_TabsConverge()
    {
        await using var tabs = await TabPair.StartAsync();
        var (a, b) = (tabs.A, tabs.B);
        a.Change(0b0001);
        b.Change(0b0010);
        await tabs.SettleAsync();

        // A's setItem lands, then B's, before either storage event is handled: B's event forwards {a}, which storage no
        // longer holds when B applies it.
        (await a.CompleteAsync()).ShouldBeTrue();
        (await b.CompleteAsync(deliverFirst: false)).ShouldBeTrue();
        tabs.Storage["ducky:tags"].ShouldContain("""["b"]""");

        // B's receipt reads the key, finds its own {b} (an echo) and restores nothing; A takes {b}.
        (await b.DeliverAsync()).ShouldBeTrue();
        b.X.Restores.Entries.ShouldBeEmpty();
        (await a.DeliverAsync()).ShouldBeTrue();
        await tabs.SettleAsync();

        (await tabs.DrainAsync()).ShouldBe(0);
        a.Items.ShouldBe(["b"]);
        b.Items.ShouldBe(["b"]);
        tabs.Storage["ducky:tags"].ShouldContain("""["b"]""");
        (a.Writes, b.Writes).ShouldBe((1, 1));
    }

    [Fact]
    public async Task CrossTab_DebounceEndsAfterOwnWrite_ReceiptReadsCurrentValue_TabsConverge()
    {
        await using var tabs = await TabPair.StartAsync();
        var (a, b) = (tabs.A, tabs.B);
        a.Change(0b0001);
        b.Change(0b0010);
        await tabs.SettleAsync();
        (await a.CompleteAsync(deliverFirst: false)).ShouldBeTrue();

        // A's event reaches B before B's setItem, and its 50 ms debounce ends after it.
        var receipt = b.BeginDelivery();
        (await b.CompleteAsync()).ShouldBeTrue();
        tabs.Storage["ducky:tags"].ShouldContain("""["b"]""");
        b.X.H.Time.Advance(CrossTabHarness.Debounce);
        await receipt.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        await tabs.SettleAsync();
        b.X.Restores.Entries.ShouldBeEmpty();

        (await tabs.DrainAsync()).ShouldBe(1);
        a.Items.ShouldBe(["b"]);
        b.Items.ShouldBe(["b"]);
        tabs.Storage["ducky:tags"].ShouldContain("""["b"]""");
        (a.Writes, b.Writes).ShouldBe((1, 1));
    }

    [Fact]
    public async Task CrossTab_ReceiptReadsOwnUnacknowledgedWrite_PendingChangeWins()
    {
        await using var tabs = await TabPair.StartAsync();
        var (a, b) = (tabs.A, tabs.B);
        a.Change(0b0001);
        b.Change(0b0010);
        await tabs.SettleAsync();
        (await a.CompleteAsync()).ShouldBeTrue();

        // B's setItem({b}) ran but its result has not reached .NET: B's baseline still lags when B changes to {d} and A's
        // receipt reads B's own {b}. It equals B's write in flight: the receipt waits for that write and reads again, and
        // never restores it. Restored, {b} would overwrite {d}, which the writer would then dedupe against the {b} baseline
        // its completion sets.
        b.Land().ShouldBeTrue();
        b.Change(0b1000);
        await tabs.SettleAsync();
        b.X.H.Middleware.LastKnownPayload.ContainsKey("ducky:tags").ShouldBeFalse();
        (await b.DeliverAsync()).ShouldBeTrue();
        b.X.Restores.Entries.ShouldBeEmpty();
        b.Items.ShouldBe(["d"]);

        // B's {b} completes, then its {d} is written and reaches A.
        (await tabs.DrainAsync()).ShouldBeGreaterThan(0);
        a.Items.ShouldBe(["d"]);
        b.Items.ShouldBe(["d"]);
        tabs.Storage["ducky:tags"].ShouldContain("""["d"]""");
        (a.Writes, b.Writes).ShouldBe((1, 2));
    }

    [Fact]
    public async Task CrossTab_ReceiptReadOvertakenByOwnWrite_Dropped_TabsConverge()
    {
        await using var tabs = await TabPair.StartAsync();
        var (a, b) = (tabs.A, tabs.B);
        a.Change(0b0001);
        await tabs.SettleAsync();
        (await a.CompleteAsync()).ShouldBeTrue();

        // B's receipt reads A's {a}, and its result reaches .NET only after B's own later {b} was written and acknowledged
        // (Blazor Server runs the continuations concurrently). JS ran the read before that setItem, so the value is stale:
        // restoring it would set the baseline to {a}, and B's writer would never rewrite its {b}. Read again, it is B's own.
        (await b.HoldReadAsync()).ShouldBeTrue();
        b.Change(0b0010);
        await tabs.SettleAsync();
        (await b.CompleteAsync()).ShouldBeTrue();
        tabs.Storage["ducky:tags"].ShouldContain("""["b"]""");
        (await b.AnswerReadAsync()).ShouldBeTrue();
        b.X.Restores.Entries.ShouldBeEmpty();
        b.Items.ShouldBe(["b"]);
        b.X.H.Middleware.LastKnownPayload["ducky:tags"].ShouldBe("""["b"]""");

        // A takes B's {b}, and nobody writes again.
        (await tabs.DrainAsync()).ShouldBe(1);
        a.Items.ShouldBe(["b"]);
        b.Items.ShouldBe(["b"]);
        tabs.Storage["ducky:tags"].ShouldContain("""["b"]""");
        (a.Writes, b.Writes).ShouldBe((1, 1));
    }

    [Fact]
    public async Task CrossTab_RemoteChangeDuringLocalPendingWrite_TabsConverge()
    {
        // Fixed scripts, run on every gated run whatever the sampled ones hit. A held write's success keeps the baseline a
        // receipt set meanwhile (what storage holds), so a fresh read writes the state only if it changed back.
        foreach (var (script, stored, writes) in _fixed)
        {
            var run = await RunAsync(script);
            Check(script, run);
            run.Stored.ShouldBe(stored, ignoreOrder: true, Describe(script));
            run.Writes.ShouldBe(writes, Describe(script));
        }

        // Property: whatever the interleaving of local changes, held writes and storage events, both tabs end on the
        // state storage holds.
        foreach (var script in Cases(_scripts))
        {
            Check(script, await RunAsync(script));
        }
    }

    // INV-18 (within the model of Tab): both tabs end on the state storage holds, and a tab writes at most once per local
    // change of its state plus once per receipt that landed while one of its writes was in flight (a receipt with none in
    // flight writes nothing, checked by Tab.DeliverAsync).
    private static void Check((int Kind, int Bits)[] script, Run run)
    {
        var described = Describe(script);
        run.A.ShouldBe(run.Stored, ignoreOrder: true, described);
        run.B.ShouldBe(run.Stored, ignoreOrder: true, described);
        run.Writes.A.ShouldBeLessThanOrEqualTo(run.Allowed.A, described);
        run.Writes.B.ShouldBeLessThanOrEqualTo(run.Allowed.B, described);
    }

    private static string Describe((int Kind, int Bits)[] script) => $"script [{string.Join(", ", script)}]";

    // CsCheck generates the cases (fixed seeds when gated); each runs as an async scenario afterwards.
    private static List<T> Cases<T>(Gen<T> gen)
    {
        ConcurrentBag<T> cases = [];
        Property.Check(gen, cases.Add);
        return [.. cases];
    }

    private static async Task<Run> RunAsync((int Kind, int Bits)[] script)
    {
        await using var tabs = await TabPair.StartAsync();
        var (a, b) = (tabs.A, tabs.B);
        foreach (var (kind, bits) in script)
        {
            switch (kind)
            {
                case 0:
                    a.Change(bits);
                    break;
                case 1:
                    b.Change(bits);
                    break;
                case 2:
                    await a.CompleteAsync();
                    break;
                case 3:
                    await b.CompleteAsync();
                    break;
                case 4:
                    await a.DeliverAsync();
                    break;
                case 5:
                    await b.DeliverAsync();
                    break;
                case 6:
                    await a.CompleteAsync(deliverFirst: false);
                    break;
                case 7:
                    await b.CompleteAsync(deliverFirst: false);
                    break;
                case 8:
                    a.Land();
                    break;
                case 9:
                    b.Land();
                    break;
                case 10:
                    await a.HoldReadAsync();
                    break;
                case 11:
                    await b.HoldReadAsync();
                    break;
                case 12:
                    await a.AnswerReadAsync();
                    break;
                default:
                    await b.AnswerReadAsync();
                    break;
            }

            await tabs.SettleAsync();
        }

        await tabs.DrainAsync();
        var stored = tabs.Storage.TryGetValue("ducky:tags", out var envelope)
            ? JsonDocument.Parse(envelope).RootElement.GetProperty("s").EnumerateArray().Select(static item => item.GetString()!).ToList()
            : [];
        return new([.. a.Items], [.. b.Items], stored, (a.Writes, b.Writes), (a.Allowed, b.Allowed));
    }

    private sealed record Run(List<string> A, List<string> B, List<string> Stored, (int A, int B) Writes, (int A, int B) Allowed);
}

internal sealed record Tags(ImmutableHashSet<string> Items);

internal sealed record SetTags(int Bits);

internal sealed class TagsSlice : Slice<Tags>
{
    public TagsSlice() => On<SetTags>(static (_, action) => new(ItemsOf(action.Bits)));

    protected override Tags Initial => new([]);

    public static ImmutableHashSet<string> ItemsOf(int bits) => [.. "abcd".Where((_, i) => ((bits >> i) & 1) == 1).Select(static letter => letter.ToString())];
}

// A set written as an array in one fixed element order: ascending ordinal in one tab, descending in the other, as two
// processes whose string hashes are seeded differently enumerate one ImmutableHashSet.
internal sealed class OrderedTagsConverter(bool descending) : JsonConverter<Tags>
{
    public override Tags Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var items = ImmutableHashSet.CreateBuilder<string>();
        while (reader.Read() && reader.TokenType == JsonTokenType.String)
        {
            items.Add(reader.GetString()!);
        }

        return new(items.ToImmutable());
    }

    public override void Write(Utf8JsonWriter writer, Tags value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in descending ? value.Items.OrderDescending(StringComparer.Ordinal) : value.Items.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}

// One browser tab: a store whose Tags slice is synced across tabs and serialized in its own element order. Every
// storageSet is held until the test completes it, which sets the shared localStorage and queues the storage event the
// other tab receives (never the writing tab, as in a browser); landing it without its result models setItem having run
// while the storageSet result is still on its way back to .NET. Events reach a tab in setItem order, each forwarding the
// value its setItem stored, which may be stale by the time the tab applies it; storageGet reads the shared localStorage
// after the setItem of the write the tab holds, if any, as ducky.js runs calls in issue order.
internal sealed class Tab
{
    private readonly ConcurrentQueue<Held> _held = new();
    private readonly ConcurrentQueue<string> _events = new();
    private readonly ConcurrentDictionary<string, string> _storage;
    private readonly Lock _gate = new();
    private HeldRead? _read;
    private Task? _waiting;
    private bool _closed;

    public Tab(bool descending, ConcurrentDictionary<string, string> storage)
    {
        _storage = storage;
        X = new(d => d.AddSlice<TagsSlice>().Persist<TagsSlice>(static o => o.SyncAcrossTabs = true).UseJson(Json(descending)));
        X.H.OnGet = Read;
        X.H.OnSet = write =>
        {
            lock (_gate)
            {
                if (_closed)
                {
                    return true;
                }

                // The writer marked the call in flight before sending it: its resolution is what a receipt waits for.
                var held = new Held(write, X.H.Middleware.Resolved[write.Key].Task);
                _held.Enqueue(held);
                return held.Done.Task;
            }
        };
    }

    public CrossTabHarness X { get; }

    public Tab Other { get; set; } = null!;

    public ImmutableHashSet<string> Items => X.H.Store.State.Get<Tags>().Items;

    public int Writes => X.H.Written.Count;

    /// <summary>Local changes of state plus receipts that landed while a write of this tab was held: the most writes it may make.</summary>
    public int Allowed { get; private set; }

    /// <summary>Its writer is idle with nothing dirty, or waits on a held write.</summary>
    public bool Settled => !_held.IsEmpty || !X.H.Middleware.Writers["tags"].Dirty;

    public bool Quiet => _held.IsEmpty && _events.IsEmpty && _read is null && _waiting is null;

    // The receipts that found their read ambiguous while a write of this tab was in flight (§11.7), so far.
    private int Waits => X.EventIds(LogLevel.Debug).Count(static id => id == 2056);

    public void Change(int bits)
    {
        // Only a change of state may be written: the bits the tab already holds reduce to an equal set the writer dedupes,
        // unless nothing is stored or in flight yet (an absent baseline writes even the initial state, §11.5).
        if (!Items.SetEquals(TagsSlice.ItemsOf(bits)) || (_held.IsEmpty && !X.H.Middleware.LastKnownPayload.ContainsKey("ducky:tags")))
        {
            Allowed++;
        }

        X.H.Store.Dispatch(new SetTags(bits));
    }

    /// <summary>
    /// Runs the oldest held write's localStorage.setItem, then queues its storage event in the other tab; its result stays
    /// held, so this tab's baseline does not record it yet. False when nothing is held or it already landed.
    /// </summary>
    public bool Land()
    {
        lock (_gate)
        {
            if (!_held.TryPeek(out var held) || held.Landed)
            {
                return false;
            }

            held.Landed = true;
            _storage[held.Write.Key] = held.Write.Value;
            Other._events.Enqueue(held.Write.Value);
            return true;
        }
    }

    /// <summary>
    /// Completes the oldest held write: unless <paramref name="deliverFirst"/> is false or its setItem already ran, the
    /// events already queued for this tab are delivered first (its JS forwards them before it runs the setItem, so they
    /// land while the write is pending, though the first storageGet runs that setItem first), then localStorage.setItem and the storage event in the other tab (<see cref="Land"/>),
    /// then its result.
    /// </summary>
    public async Task<bool> CompleteAsync(bool deliverFirst = true)
    {
        if (!_held.TryPeek(out var held))
        {
            return false;
        }

        while (deliverFirst && !held.Landed && _events.TryDequeue(out var value))
        {
            Allowed++;
            await ReceiveAsync(value);
        }

        Land();
        _held.TryDequeue(out _);

        // A receipt waiting for this write reads again once it resolved: that read is held until the writer handled the
        // result and sent its next write, if any, so the step is deterministic (a read sent before that write would be
        // stale, and read again after it: answering it later reads what that second read would).
        var waiting = _waiting;
        var answer = X.H.OnGet;
        TaskCompletionSource<object?> reread = new(TaskCreationOptions.RunContinuationsAsynchronously);
        object?[]? asked = null;
        if (waiting is not null)
        {
            X.H.OnGet = args =>
            {
                X.H.OnGet = answer;
                asked = args;
                return reread.Task;
            };
        }

        held.Done.SetResult(true);

        // Its result is handled (the baseline set to what it wrote, unless a receipt replaced it meanwhile, and the call
        // resolved) before the next step runs: the writer is idle, or holds its next write.
        await WriterHarness.Until(() => held.Resolved.IsCompleted && Settled);
        if (waiting is not null)
        {
            // Superseded by a later receipt, it ends without reading.
            await WriterHarness.Until(() => asked is not null || waiting.IsCompleted);
            if (asked is null)
            {
                X.H.OnGet = answer;
                _waiting = null;
            }
            else
            {
                var waits = Waits;
                reread.SetResult(Read(asked));
                await TrackAsync(waiting, waits);
            }
        }

        return true;
    }

    /// <summary>
    /// Delivers this tab's next storage event, if any, and waits until it was handled. With no write of this tab in flight
    /// the receipt must not write at all (<c>NoWrite_ForHydrationCrossTabDevToolsOrigins</c>), checked on every receipt.
    /// </summary>
    public async Task<bool> DeliverAsync()
    {
        if (!_events.TryDequeue(out var value))
        {
            return false;
        }

        var idle = _held.IsEmpty;
        var before = Writes;
        if (!idle)
        {
            Allowed++;
        }

        await ReceiveAsync(value);
        if (idle)
        {
            await WriterHarness.Until(() => Settled);
            Writes.ShouldBe(before, "a receipt with no write in flight triggered a write");
        }

        return true;
    }

    /// <summary>Hands this tab's next storage event to its CrossTabSync without letting its debounce elapse.</summary>
    public Task BeginDelivery()
    {
        Allowed++;
        _events.TryDequeue(out var value);
        return X.Sync.OnStorageChangedAsync("tab", "local", "ducky:tags", value, tooLarge: false);
    }

    /// <summary>
    /// Hands this tab's next storage event to its CrossTabSync and lets its debounce end: its storageGet reads localStorage
    /// now (<see cref="Read"/>), but its result stays held until <see cref="AnswerReadAsync"/>. False when no event is
    /// queued or a read is already held.
    /// </summary>
    public async Task<bool> HoldReadAsync()
    {
        if (_read is not null || !_events.TryDequeue(out var value))
        {
            return false;
        }

        var answer = X.H.OnGet;
        var read = new HeldRead();
        X.H.OnGet = args =>
        {
            X.H.OnGet = answer;
            read.Value = Read(args);
            read.Asked = true;
            return read.Result.Task;
        };
        _read = read;
        read.Receipt = X.Sync.OnStorageChangedAsync("tab", "local", "ducky:tags", value, tooLarge: false);
        X.H.Time.Advance(CrossTabHarness.Debounce);
        await WriterHarness.Until(() => read.Asked);
        return true;
    }

    /// <summary>Answers the held storageGet with the value it read, and waits until its receipt was handled; false when none is held.</summary>
    public async Task<bool> AnswerReadAsync()
    {
        if (_read is not { } read)
        {
            return false;
        }

        // Counted like a delivered event: a receipt with no write in flight must not write (DeliverAsync).
        var idle = _held.IsEmpty;
        var before = Writes;
        if (!idle)
        {
            Allowed++;
        }

        _read = null;
        var waits = Waits;
        read.Result.SetResult(read.Value);
        await TrackAsync(read.Receipt, waits);
        if (idle)
        {
            await WriterHarness.Until(() => Settled);
            Writes.ShouldBe(before, "a held receipt with no write in flight triggered a write");
        }

        return true;
    }

    // Hands one storage event to this tab's CrossTabSync and lets its debounce end; then TrackAsync.
    private async Task ReceiveAsync(string? value)
    {
        var waits = Waits;
        var receipt = X.Sync.OnStorageChangedAsync("tab", "local", "ducky:tags", value, tooLarge: false);
        X.H.Time.Advance(CrossTabHarness.Debounce);
        await TrackAsync(receipt, waits);
    }

    // Waits until the receipt was handled, or waits for this tab's write in flight (§11.7): CompleteAsync resumes it then.
    // Only the newest receipt of the key can wait again: an older one ends without reading once its wait is over.
    private async Task TrackAsync(Task receipt, int waits)
    {
        await WriterHarness.Until(() => receipt.IsCompleted || Waits > waits);
        _waiting = receipt.IsCompleted ? null : receipt;
    }

    // storageGet, as ducky.js runs it: after the setItem of the write this tab holds, if any (sent before the read, so JS
    // runs it first), then the shared localStorage.
    private string? Read(object?[] args)
    {
        Land();
        return _storage.GetValueOrDefault((string)args[1]!);
    }

    /// <summary>Answers every held and later write, and a held read, at once, so a dispose after a failed assertion never waits on one.</summary>
    public void Close()
    {
        _read?.Result.TrySetResult(_read.Value);
        lock (_gate)
        {
            _closed = true;
            while (_held.TryDequeue(out var held))
            {
                held.Done.SetResult(true);
            }
        }
    }

    // One storageGet whose result the test answers, with the value localStorage held when it ran.
    private sealed class HeldRead
    {
        public TaskCompletionSource<object?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? Value { get; set; }

        public bool Asked { get; set; }

        public Task Receipt { get; set; } = Task.CompletedTask;
    }

    // One storageSet the test answers; at most one per tab is held, the writer awaiting it.
    private sealed class Held(StoredWrite write, Task resolved)
    {
        public StoredWrite Write => write;

        public Task Resolved => resolved;

        public TaskCompletionSource<object?> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Landed { get; set; }
    }

    private static JsonSerializerOptions Json(bool descending) => new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(WriterJson.Default, ConvergenceJson.Default),
        Converters = { new OrderedTagsConverter(descending) },
    };
}

// Two tabs of one browser over one localStorage: A writes sets in ascending order, B in descending order.
internal sealed class TabPair : IAsyncDisposable
{
    // ponytail: a ping-pong bound, far above what any script of the property needs to settle.
    private const int MaxDrainSteps = 200;

    private TabPair()
    {
        A = new(descending: false, Storage);
        B = new(descending: true, Storage) { Other = A };
        A.Other = B;
    }

    public ConcurrentDictionary<string, string> Storage { get; } = new();

    public Tab A { get; }

    public Tab B { get; }

    public static async Task<TabPair> StartAsync()
    {
        var pair = new TabPair();
        await pair.A.X.H.InitializeAsync();
        await pair.B.X.H.InitializeAsync();
        await pair.SettleAsync();
        return pair;
    }

    /// <summary>Waits until both writers are idle or blocked on a held write.</summary>
    public Task SettleAsync() => WriterHarness.Until(() => A.Settled && B.Settled);

    /// <summary>Delivers every event and completes every held write until both tabs are quiet; returns the steps taken.</summary>
    public async Task<int> DrainAsync()
    {
        var steps = 0;
        while (!(A.Quiet && B.Quiet))
        {
            (++steps).ShouldBeLessThanOrEqualTo(MaxDrainSteps, "the tabs never settled: they keep writing to each other");
            _ = await A.AnswerReadAsync() || await B.AnswerReadAsync() || await A.DeliverAsync() || await B.DeliverAsync()
                || await A.CompleteAsync() || await B.CompleteAsync();
            await SettleAsync();
        }

        return steps;
    }

    public async ValueTask DisposeAsync()
    {
        A.Close();
        B.Close();
        await A.X.DisposeAsync();
        await B.X.DisposeAsync();
    }
}

[JsonSerializable(typeof(Tags))]
internal sealed partial class ConvergenceJson : JsonSerializerContext;
