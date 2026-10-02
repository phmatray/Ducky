using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.InitFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §5.2 (IStore.Select, Selection<T>), §6.8 (Select order, the drainer's CAS-or-compare), §6.4 step 10, §6.11
// (after disposal); INV-13, INV-21. Isolation of throwing subscribers and the _disposed flag come with M3-01b.
public sealed class SelectTests
{
    private sealed record Box(int Value);

    // Records the `last` argument of every comparison, so a test can tell which selector call installed it.
    private sealed class RecordingComparer : IEqualityComparer<Box>
    {
        public List<Box> Lasts { get; } = [];

        public bool Equals(Box? x, Box? y)
        {
            Lasts.Add(x!);
            return EqualityComparer<Box>.Default.Equals(x, y);
        }

        public int GetHashCode(Box obj) => obj.GetHashCode();
    }

    [Fact]
    public async Task Select_CommitBetweenAddAndRead_InstalledByDrainer()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        List<Box> evaluated = [];
        List<Box> changes = [];
        var comparer = new RecordingComparer();

        // The hook runs between the add and the read, on this thread, which becomes the drainer: it installs `last`
        // from Unset without calling onChange, so the registering thread's CAS then fails.
        store.AfterSubscribeHook = () => store.Dispatch(new Bump());
        using var selection = store.Select(
            s =>
            {
                var box = new Box(s.Get<Count>().Value);
                evaluated.Add(box);
                return box;
            },
            changes.Add,
            comparer);

        changes.ShouldBeEmpty();
        evaluated.Count.ShouldBe(2);
        evaluated[1].ShouldBe(new Box(1));

        store.AfterSubscribeHook = null;
        store.Dispatch(new Bump());

        // `last` is the drainer's instance, not the registering thread's equal one.
        comparer.Lasts.ShouldHaveSingleItem().ShouldBeSameAs(evaluated[0]);
        changes.ShouldBe([new Box(2)]);
    }

    [Fact]
    public async Task Select_DedupesOnProjectedValue()
    {
        var store = new DuckyStore([new CountSlice(), new BoomSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        List<int> changes = [];
        using var selection = store.Select(s => s.Get<Count>().Value, changes.Add);

        store.Dispatch(new Ping()); // a commit that leaves the projection equal
        changes.ShouldBeEmpty();

        store.Dispatch(new Bump());
        store.Dispatch(new Ping());
        store.Dispatch(new Bump());
        changes.ShouldBe([1, 2]);
    }

    // Non-normative: after disposal Select returns an inert Selection whose Value reads the last snapshot and holds no
    // subscription (§6.11).
    [Fact]
    public async Task Select_AfterDispose_InertReadsLastSnapshot()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
        await store.DisposeAsync();
        List<int> changes = [];

        using var selection = store.Select(s => s.Get<Count>().Value, changes.Add);
        store.Dispatch(new Bump());

        selection.Value.ShouldBe(1);
        changes.ShouldBeEmpty();
        store.Dispatcher.HasSubscribers.ShouldBeFalse();
    }

    // Non-normative: Select starts init (§6.7, INV-13), even without onChange and before Value is read.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Select_StartsInit(bool withOnChange)
    {
        var gate = new InitGate();
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance, middleware: () => [gate]);

        using var selection = store.Select(s => s.Get<Count>().Value, withOnChange ? _ => { } : null);

        gate.Started.ShouldBe(1);
    }

    // Non-normative: Value evaluates the selector against the current snapshot on every read; without onChange nothing
    // runs on the drainer; a commit that changes nothing notifies no one (§6.4 step 10, §6.8).
    [Fact]
    public async Task Select_Value_EvaluatesOnReadAndDrainerRunsOnlyOnChangedCommits()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var plainRuns = 0;
        var watchedRuns = 0;
        using var plain = store.Select(s => ++plainRuns + (s.Get<Count>().Value * 100));
        using var watched = store.Select(s => ++watchedRuns + s.Get<Count>().Value, _ => { });
        plainRuns.ShouldBe(0);
        watchedRuns.ShouldBe(1);

        store.Dispatch(new Probe()); // reduces, but commits no change
        store.Dispatch(new Bump());

        plainRuns.ShouldBe(0);
        watchedRuns.ShouldBe(2);
        plain.Value.ShouldBe(101);
        ((int)plain).ShouldBe(102);
        plain.ToString().ShouldBe("103");
    }

    // Non-normative: a custom comparer decides what counts as a change.
    [Fact]
    public async Task Select_CustomComparer_DecidesChange()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        List<string> changes = [];
        using var selection = store.Select(
            s => s.Get<Count>().Value < 2 ? "low" : "LOW",
            changes.Add,
            StringComparer.OrdinalIgnoreCase);

        store.Dispatch(new Bump());
        store.Dispatch(new Bump());

        changes.ShouldBeEmpty();
    }

    // Non-normative: Dispose unsubscribes, idempotently; the other subscriptions keep their callbacks.
    [Fact]
    public async Task Select_Dispose_UnsubscribesIdempotently()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        List<int> first = [];
        List<int> second = [];
        var selection = store.Select(s => s.Get<Count>().Value, first.Add);
        using var other = store.Select(s => s.Get<Count>().Value, second.Add);

        selection.Dispose();
        selection.Dispose();
        store.Dispatch(new Bump());

        first.ShouldBeEmpty();
        second.ShouldBe([1]);
        selection.Value.ShouldBe(1);
    }

    [Fact]
    public void Select_NullSelector_Throws()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);

        Should.Throw<ArgumentNullException>(() => store.Select<int>(null!)).ParamName.ShouldBe("selector");
    }

    // Non-normative: Selection.Create, for extension packages: Value calls read then onRead; Dispose calls onDispose once.
    [Fact]
    public void Selection_Create_ReadsThenOnReadAndDisposesOnce()
    {
        List<string> calls = [];
        var next = 0;
        var selection = Selection<int>.Create(
            () =>
            {
                calls.Add("read");
                return ++next;
            },
            value => calls.Add($"onRead {value}"),
            () => calls.Add("dispose"));

        selection.Value.ShouldBe(1);
        selection.Dispose();
        selection.Dispose();

        calls.ShouldBe(["read", "onRead 1", "dispose"]);
    }

    // Non-normative: the optional callbacks may be omitted; ToString of a null value is null.
    [Fact]
    public void Selection_Create_WithoutCallbacks()
    {
        var selection = Selection<string?>.Create(() => null);

        selection.Value.ShouldBeNull();
        selection.ToString().ShouldBeNull();
        selection.Dispose();
        Should.Throw<ArgumentNullException>(() => Selection<int>.Create(null!)).ParamName.ShouldBe("read");
    }

    // Non-normative: a selector that throws on the registration read (§6.8 step 3) fails Select and leaves nothing
    // subscribed, so no later commit calls back into a caller that never got a Selection.
    [Fact]
    public async Task Select_SelectorThrowsAtRegistration_Unsubscribes()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        List<int> changes = [];

        Should.Throw<InvalidOperationException>(() => store.Select(
            s => s.Get<Count>().Value == 0 ? throw new InvalidOperationException("empty") : s.Get<Count>().Value,
            changes.Add));
        store.Dispatch(new Bump());
        store.Dispatch(new Bump());

        changes.ShouldBeEmpty();
        store.Dispatcher.HasSubscribers.ShouldBeFalse();
    }

    // Non-normative: disposal releases every subscription and its onChange closure (§6.11 step 6).
    [Fact]
    public async Task Dispose_ClearsSubscribers()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        using var selection = store.Select(s => s.Get<Count>().Value, _ => { });
        store.Dispatcher.HasSubscribers.ShouldBeTrue();

        await store.DisposeAsync();

        store.Dispatcher.HasSubscribers.ShouldBeFalse();
    }

    // Non-normative: when the drain overruns DisposeTimeout, step 6 is chained on the drain's exit, never run while the
    // drain may still notify (§6.11).
    [Fact]
    public async Task Dispose_DrainOverrunsDisposeTimeout_ClearsSubscribersOnDrainExit()
    {
        var slice = new CountSlice();
        var store = new DuckyStore([slice], NullLogger.Instance, disposeTimeout: TimeSpan.FromSeconds(-1));
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        using var selection = store.Select(s => s.Get<Count>().Value, _ => { });
        Task? disposal = null;
        var heldInDrain = false;
        slice.OnProbe = () =>
        {
            disposal = store.DisposeAsync().AsTask();
            heldInDrain = disposal.IsCompleted && store.Dispatcher.HasSubscribers;
        };

        store.Dispatch(new Probe());

        heldInDrain.ShouldBeTrue();
        await disposal.ShouldNotBeNull();
        await store.Dispatcher.DisposalSteps.ShouldNotBeNull();
        store.Dispatcher.HasSubscribers.ShouldBeFalse();
    }
}
