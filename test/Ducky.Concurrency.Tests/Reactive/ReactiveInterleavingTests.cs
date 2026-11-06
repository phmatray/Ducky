using System.Collections.Concurrent;
using Ducky.Reactive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using R3;

namespace Ducky.Concurrency.Tests;

internal sealed record Tally(int Value);

internal sealed record Bump;

// Bumped like Bump, but parks the drainer between its commit and the bridge's State.Value write (ParkMiddleware).
internal sealed record Park;

internal sealed class TallySlice : Slice<Tally>
{
    public TallySlice()
    {
        On<Bump>(static s => new(s.Value + 1));
        On<Park>(static s => new(s.Value + 1));
    }

    protected override Tally Initial => new(0);
}

// What the pipelines saw, written from the drainer, timer and pool threads. Every state change bumps the tally, so a
// subscription's values must strictly increase, and its first one must lie between the live state read just before it
// subscribed and just after its first value arrived.
internal sealed class RaceProbe
{
    private int _subscriptions;
    private int _expected;
    private int _lost;
    private int _witnessBumps;
    private int _witnessLast = -1;

    public IStore Store { get; set; } = null!;

    public Barrier Park { get; init; } = null!;

    public CancellationToken Token { get; set; }

    public ConcurrentQueue<string> Violations { get; } = new();

    // Inner actions subscriptions that joined the subject before the last commit, and those of them that never received
    // an action (counted at the store's dispose, when every inner is torn down).
    public int Expected => Volatile.Read(ref _expected);

    public int Lost => Volatile.Read(ref _lost);

    public int Subscriptions => Volatile.Read(ref _subscriptions);

    public int WitnessBumps => Volatile.Read(ref _witnessBumps);

    public int WitnessLast => Volatile.Read(ref _witnessLast);

    public void Subscribed() => Interlocked.Increment(ref _subscriptions);

    public void WitnessBump() => Interlocked.Increment(ref _witnessBumps);

    public void WitnessState(StateSnapshot state) => Volatile.Write(ref _witnessLast, Value(state));

    public static int Value(StateSnapshot state) => state.Get<Tally>().Value;

    public Observable<StateSnapshot> Checked(Observable<StateSnapshot> state) =>
        Observable.Defer(() =>
        {
            var before = Value(Store.State);
            var last = -1;
            return state.Do(s =>
            {
                var seen = Value(s);
                if (last < 0)
                {
                    var after = Value(Store.State);
                    if (seen < before || seen > after)
                    {
                        Violations.Enqueue($"first value {seen} outside [{before}, {after}]");
                    }
                }
                else if (seen <= last)
                {
                    Violations.Enqueue($"value {seen} after {last}");
                }

                last = seen;
            });
        });

    // An inner actions.Take(1). The tally read once it has joined the subject is a commit this subscriber is behind: the
    // next commit's action reaches it unless the subject lost it, while the producer's OnNext ran and other threads joined
    // and left. Final means nothing is left to deliver, so it expects nothing.
    public Observable<object> Delivered(Observable<object> actions, int final) =>
        Observable.Create<object>(observer =>
        {
            var received = 0;
            var subscription = actions.Take(1).Do(_ => Volatile.Write(ref received, 1)).Subscribe(observer);
            if (Value(Store.State) >= final)
            {
                return subscription;
            }

            Interlocked.Increment(ref _expected);
            return Disposable.Create(() =>
            {
                if (Volatile.Read(ref received) == 0)
                {
                    Interlocked.Increment(ref _lost);
                }

                subscription.Dispose();
            });
        });
}

// Fails a millisecond after each subscription and resubscribes a millisecond later, on the thread advancing the clock, so
// it leaves and rejoins both the state property and the actions subject there while the producer's drain pushes.
internal sealed class ResubscribingEffect(RaceProbe probe, TimeProvider time) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state)
    {
        probe.Subscribed();
        return Observable.Merge(
            probe.Checked(state).Where(static _ => false).Select(static object (s) => s),
            actions.Where(static _ => false),
            Observable.Timer(TimeSpan.FromMilliseconds(1), time).Select(static object (_) => throw new InvalidOperationException("tick")).OnErrorResumeAsFailure());
    }

    protected override TimeSpan? RetryDelay(int attempt) => TimeSpan.FromMilliseconds(1);
}

// The polling shape: each timer tick subscribes an inner state.Take(1) and an inner actions.Take(1) on the thread
// advancing the clock; the drainer disposes the latter in the OnNext that completes it.
internal sealed class InnerStateEffect(RaceProbe probe, TimeProvider time) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Interval(TimeSpan.FromMilliseconds(1), time)
            .SelectMany(_ => Observable.Merge(probe.Checked(state).Take(1).Select(static object (s) => s), probe.Delivered(actions, ReactiveInterleavingTests.Final)))
            .Where(static _ => false);
}

// Subscribed throughout: it must see every Bump and the final state, whatever subscribed and left meanwhile.
internal sealed class WitnessEffect(RaceProbe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Merge(
            actions.OfActionType<Bump>().Do(_ => probe.WitnessBump()).Select(static object (a) => a),
            state.Do(probe.WitnessState).Select(static object (s) => s))
            .Where(static _ => false);
}

// Registered before ReactiveMiddleware: on Park, after the commit and before the bridge's write, it hands over to the
// clock thread and waits until that thread is done.
internal sealed class ParkMiddleware(RaceProbe probe) : Middleware
{
    public override void AfterReduce(ActionContext context)
    {
        if (context.Action is Park)
        {
            probe.Park.SignalAndWait(probe.Token);
            probe.Park.SignalAndWait(probe.Token);
        }
    }
}

// What the dispose race saw: the retry parks in Handle until the effect is disposed, or Window passes.
internal sealed class DisposeRace : IDisposable
{
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(500);

    public ManualResetEventSlim InRetry { get; } = new();

    public ManualResetEventSlim Disposed { get; } = new();

    public bool DisposedDuringHandle { get; set; }

    public void Dispose()
    {
        InRetry.Dispose();
        Disposed.Dispose();
    }
}

// Its first subscription fails at once; the retry, a millisecond later on the clock thread, parks in Handle.
internal sealed class ParkedRetryEffect(DisposeRace race) : ReactiveEffect, IDisposable
{
    private int _handles;

    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state)
    {
        if (++_handles == 1)
        {
            return Observable.Throw<object>(new InvalidOperationException("first"));
        }

        race.InRetry.Set();
        race.DisposedDuringHandle = race.Disposed.Wait(DisposeRace.Window);
        return Observable.Never<object>();
    }

    public void Dispose() => race.Disposed.Set();

    protected override TimeSpan? RetryDelay(int attempt) => TimeSpan.FromMilliseconds(1);
}

// SPEC §14, §17.3 row 20: INV-27. Failure resubscribes and timer-driven inner subscriptions race a producer's drain.
[Collection(nameof(Interleaving))]
public sealed class ReactiveInterleavingTests
{
    internal const int Final = Bumps + 1;
    private const int Bumps = 200;
    private const int Ticks = 200;

    private static Task Dedicated(Action action, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    // First, the drainer parks between Park's commit and the bridge's write while the clock thread fails and resubscribes
    // the pipeline and ticks the inner subscriptions: each must start at the committed state. Then the producer dispatches
    // while the clock thread keeps resubscribing and ticking. No subscription ever starts behind the live state or goes
    // back, the witness misses nothing, and no inner actions subscriber behind the last commit misses an action, while
    // the clock thread joins and leaves the actions subject during the producer's OnNext. Its deterministic twin is
    // ReactiveFailureTests.…_Deterministic. A thread that creates a timer while FakeTimeProvider's wake gate is free fires
    // the due timers itself, so in the second phase callbacks also run on the producer, and how many resubscribes it sees
    // varies: only the first phase's is asserted.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Reactive_ResubscribeConcurrentWithDrain_FirstValueCurrent_NoLostSubscriber(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        using var park = new Barrier(2);
        var probe = new RaceProbe { Park = park, Token = cancellationToken };
        await using var provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(time)
            .AddSingleton(probe)
            .AddDucky(d => d.AddSlice<TallySlice>()
                .Use<ParkMiddleware>()
                .AddReactiveEffect<ResubscribingEffect>()
                .AddReactiveEffect<InnerStateEffect>()
                .AddReactiveEffect<WitnessEffect>())
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        probe.Store = store;
        await Interleaving.Within(store.InitializeAsync(cancellationToken));

        var resubscribedWhileParked = false;
        var producer = Dedicated(
            () =>
            {
                store.Dispatch(new Park());
                for (var i = 0; i < Bumps; i++)
                {
                    store.Dispatch(new Bump());
                }
            },
            cancellationToken);
        var clock = Dedicated(
            () =>
            {
                // finally: a throw here must release the parked drainer, so Within reports it, not a deadlock.
                probe.Park.SignalAndWait(cancellationToken);
                try
                {
                    var subscribed = probe.Subscriptions;
                    for (var i = 0; i < 4 && probe.Subscriptions == subscribed; i++)
                    {
                        time.Advance(TimeSpan.FromMilliseconds(1));
                    }

                    resubscribedWhileParked = probe.Subscriptions > subscribed;
                }
                finally
                {
                    probe.Park.SignalAndWait(cancellationToken);
                }

                for (var i = 0; i < Ticks; i++)
                {
                    time.Advance(TimeSpan.FromMilliseconds(1));
                }
            },
            cancellationToken);
        await Interleaving.Within(Task.WhenAll(producer, clock));
        await Interleaving.Within(store.WhenIdleAsync(cancellationToken));
        var witnessBumps = probe.WitnessBumps;
        var witnessLast = probe.WitnessLast;
        await Interleaving.Within(scope.DisposeAsync().AsTask());

        resubscribedWhileParked.ShouldBeTrue();
        probe.Violations.ShouldBeEmpty();
        RaceProbe.Value(store.State).ShouldBe(Final);
        witnessBumps.ShouldBe(Bumps);
        witnessLast.ShouldBe(Final);
        probe.Expected.ShouldBeGreaterThan(0);
        probe.Lost.ShouldBe(0);
    }

    // §14, INV-27, non-normative: a retry already past the disposed-slot check when the store is disposed. Phase 5b waits
    // for its Handle to return before disposing the effect, so Handle never runs on a disposed instance. Without the wait
    // the effect is disposed within milliseconds and Handle sees it; Window only bounds how long a correct run parks, so
    // a slow runner can only hide the bug, never fail a correct run.
    [Fact]
    public async Task Reactive_RetryRacingDispose_HandleNeverRunsOnDisposedEffect()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        using var race = new DisposeRace();
        await using var provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(time)
            .AddSingleton(race)
            .AddDucky(d => d.AddSlice<TallySlice>().AddReactiveEffect<ParkedRetryEffect>())
            .BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        await Interleaving.Within(scope.ServiceProvider.GetRequiredService<IStore>().InitializeAsync(cancellationToken));

        var retry = Dedicated(() => time.Advance(TimeSpan.FromMilliseconds(1)), cancellationToken);
        await Interleaving.Within(Dedicated(() => race.InRetry.Wait(cancellationToken), cancellationToken));
        await Interleaving.Within(Dedicated(() => scope.DisposeAsync().AsTask().GetAwaiter().GetResult(), cancellationToken));
        await Interleaving.Within(retry);

        race.DisposedDuringHandle.ShouldBeFalse();
        race.Disposed.IsSet.ShouldBeTrue();
    }
}
