using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §6.4 step 10, §6.8 (per-subscription isolation, the volatile _disposed flag, BeforeNotifyHook); INV-09, INV-02.
public sealed class SubscriberTests
{
    private sealed record Journal;

    // Every Step it reduces, in processing order; it never changes Count, so a Step notifies no one.
    private sealed class StepJournal : Slice<Journal>
    {
        public StepJournal() => On<Step>((state, step) =>
        {
            Steps.Add(step.Name);
            return state;
        });

        public List<string> Steps { get; } = [];

        protected override Journal Initial => new();
    }

    // Throws once, on its first comparison; equal by value otherwise.
    private sealed class ThrowOnceComparer : IEqualityComparer<int>
    {
        public int Calls { get; private set; }

        public bool Equals(int x, int y) => ++Calls == 1 ? throw new InvalidOperationException("comparer") : x == y;

        public int GetHashCode(int obj) => obj;
    }

    [Fact]
    public async Task Subscriber_UnsubscribesItselfDuringNotify()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        List<int> first = [];
        List<int> self = [];
        List<int> last = [];
        List<int> late = [];
        var victimRuns = 0;
        List<int> victimChanges = [];
        Selection<int>? selfSelection = null;
        Selection<int>? lateSelection = null;
        Selection<int>? victim = null;
        using var a = store.Select(s => s.Get<Count>().Value, value =>
        {
            first.Add(value);
            victim!.Dispose(); // a later subscription in the array this pass captured: only the flag can skip it
            lateSelection ??= store.Select(s => s.Get<Count>().Value, late.Add); // subscribing during notify
        });
        selfSelection = store.Select(s => s.Get<Count>().Value, value =>
        {
            self.Add(value);
            selfSelection!.Dispose();
        });
        victim = store.Select(s => ++victimRuns + s.Get<Count>().Value, victimChanges.Add);
        using var c = store.Select(s => s.Get<Count>().Value, last.Add);
        victimRuns.ShouldBe(1);

        store.Dispatch(new Bump());
        store.Dispatch(new Bump());

        first.ShouldBe([1, 2]);
        self.ShouldBe([1]);
        victimRuns.ShouldBe(1); // its selector never ran again after Install
        victimChanges.ShouldBeEmpty();
        last.ShouldBe([1, 2]); // the one after it in the captured array is still notified
        late.ShouldBe([2]); // added during the first notification, installed at 1, notified of the next change only
        lateSelection.ShouldNotBeNull().Dispose();
    }

    [Fact]
    public async Task Subscriber_Throws_OthersStillNotified_StaysSubscribed()
    {
        var logger = new FakeLogger();
        var store = new DuckyStore([new CountSlice(), new BoomSlice()], logger);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var thrown = new InvalidOperationException("onChange");
        List<int> before = [];
        List<int> throwing = [];
        List<int> after = [];
        var comparer = new ThrowOnceComparer();
        List<int> compared = [];
        using var a = store.Select(s => s.Get<Count>().Value, before.Add);
        using var b = store.Select(s => s.Get<Count>().Value, value =>
        {
            throwing.Add(value);
            if (throwing.Count == 1)
            {
                throw thrown;
            }
        });
        using var d = store.Select(s => s.Get<Count>().Value, compared.Add, comparer);
        using var c = store.Select(s => s.Get<Count>().Value, after.Add);

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);

        before.ShouldBe([1]);
        after.ShouldBe([1]);
        throwing.ShouldBe([1]);
        compared.ShouldBeEmpty();
        var logs = logger.Collector.GetSnapshot();
        logs.Count.ShouldBe(2);
        logs[0].Exception.ShouldBeSameAs(thrown);
        logs[1].Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("comparer");
        foreach (var log in logs)
        {
            log.Id.Id.ShouldBe(1060);
            log.Level.ShouldBe(LogLevel.Error);
            log.Message.ShouldBe($"A subscriber threw while notified of {typeof(Bump)}; it stays subscribed");
        }

        // Still subscribed, with `last` unchanged at 0: a commit that leaves the projection at 1 notifies both again.
        store.Dispatch(new Ping());

        throwing.ShouldBe([1, 1]);
        compared.ShouldBe([1]);
        before.ShouldBe([1]);
        after.ShouldBe([1]);
    }

    [Fact]
    public async Task Subscriber_SelectorThrows_OthersNotifiedAndEffectsStillStart()
    {
        var logger = new FakeLogger();
        List<Bump> handled = [];
        var effect = new Handler<Bump>((bump, _, _) =>
        {
            handled.Add(bump);
            return Task.CompletedTask;
        });
        var store = new DuckyStore([new CountSlice()], logger, effects: () => [(effect, false)]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var thrown = new InvalidOperationException("selector");
        List<int> throwing = [];
        List<int> other = [];
        using var a = store.Select(s => s.Get<Count>().Value == 1 ? throw thrown : s.Get<Count>().Value, throwing.Add);
        using var b = store.Select(s => s.Get<Count>().Value, other.Add);

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);

        throwing.ShouldBeEmpty();
        other.ShouldBe([1]);
        handled.ShouldHaveSingleItem();
        var log = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        log.Id.Id.ShouldBe(1060);
        log.Exception.ShouldBeSameAs(thrown);

        // `last` stayed 0 and the subscription stayed: the next change reaches it.
        store.Dispatch(new Bump());

        throwing.ShouldBe([2]);
        other.ShouldBe([1, 2]);
        handled.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Subscriber_DisposedBeforeNotify_NotInvoked()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var disposedRuns = 0;
        List<int> disposedChanges = [];
        List<int> kept = [];
        using var a = store.Select(s => s.Get<Count>().Value, kept.Add);
        var disposed = store.Select(s => ++disposedRuns + s.Get<Count>().Value, disposedChanges.Add);
        disposedRuns.ShouldBe(1);

        // Between the capture and the iteration: the captured array still holds it, only the flag can skip it.
        store.Dispatcher.BeforeNotifyHook = disposed.Dispose;
        store.Dispatch(new Bump());

        disposedRuns.ShouldBe(1);
        disposedChanges.ShouldBeEmpty();
        kept.ShouldBe([1]);
    }

    [Fact]
    public async Task Dispatch_ReentrantFromSubscriberAndEffectPrefix_EachProcessedOnce()
    {
        var journal = new StepJournal();
        var slice = new CountSlice();
        Task<DispatchResult>? fromEffect = null;
        var effect = new Handler<Bump>((_, context, _) =>
        {
            fromEffect ??= context.DispatchAsync(new Step("effect")); // synchronous prefix, on the drainer
            return Task.CompletedTask;
        });
        var store = new DuckyStore([slice, journal], NullLogger.Instance, effects: () => [(effect, false)]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        Task<DispatchResult>? fromSubscriber = null;
        List<int> read = [];
        using var selection = store.Select(s => s.Get<Count>().Value, value =>
        {
            read.Add(store.State.Get<Count>().Value); // reads state from inside the notification
            fromSubscriber ??= store.DispatchAsync(new Step("subscriber"));
        });

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);

        (await fromSubscriber.ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        (await fromEffect.ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        slice.Reduced.ShouldBe([nameof(Bump)]);
        journal.Steps.ShouldBe(["subscriber", "effect"]); // step 10 enqueued before step 11, each processed once
        read.ShouldBe([1]);
    }
}
