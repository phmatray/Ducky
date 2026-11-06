using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using R3;

namespace Ducky.Reactive.Tests;

public sealed record Ping;

public sealed record Search(string Term);

public sealed record Searched(string Term);

public sealed record Tick;

public sealed record Load;

public sealed record Loaded;

// FlakyEffect's triggers: a failure completion, a non-terminal error, and an emission (SPEC §14).
public sealed record Boom;

public sealed record Hiccup;

public sealed record Echo;

// A reducer failure: CounterSlice throws on it.
public sealed record Crash;

public sealed record LoadItem(int Id);

public sealed record ItemLoaded(int Id);

// ADR-0008: OfActionType<BaseAction> must not match a DerivedAction.
public record BaseAction;

public sealed record DerivedAction : BaseAction;

public sealed record Counter(int Pings, string? Searched, int Ticks, bool Loaded);

public sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice()
    {
        On<Ping>(static s => s with { Pings = s.Pings + 1 });
        On<Searched>(static (s, a) => s with { Searched = a.Term });
        On<Tick>(static s => s with { Ticks = s.Ticks + 1 });
        On<Loaded>(static s => s with { Loaded = true });

        // Handled, so a strict TestStore only flags what the pipeline publishes.
        On<Boom>(static s => s);
        On<Hiccup>(static s => s);
        On<Echo>(static s => s);
        On<Crash>(static Counter (_) => throw new InvalidOperationException("crash"));
    }

    protected override Counter Initial => new(0, null, 0, false);
}

public sealed class EffectFailureSlice : Slice<IReadOnlyList<EffectFailed>>
{
    public EffectFailureSlice() => On<EffectFailed>(static (s, a) => [.. s, a]);

    protected override IReadOnlyList<EffectFailed> Initial => [];
}

// INV-12: a reducer that throws on a failure action fails under it, so its ReducerFailed is logged only.
public sealed record EffectFailedTrap;

public sealed class ThrowOnEffectFailedSlice : Slice<EffectFailedTrap>
{
    public ThrowOnEffectFailedSlice() => On<EffectFailed>(static (_, _) => throw new InvalidOperationException("trap"));

    protected override EffectFailedTrap Initial => new();
}

public sealed class ItemsSlice : Slice<IReadOnlyList<int>>
{
    public ItemsSlice() => On<ItemLoaded>(static (s, a) => [.. s, a.Id]);

    protected override IReadOnlyList<int> Initial => [];
}

public sealed class FailureSlice : Slice<IReadOnlyList<ReducerFailed>>
{
    public FailureSlice() => On<ReducerFailed>(static (s, a) => [.. s, a]);

    protected override IReadOnlyList<ReducerFailed> Initial => [];
}

// What the reactive effects saw: they are created by ReactiveHost, never resolvable, so they report here.
public sealed class Probe
{
    public List<object> Seen { get; } = [];

    public int Emissions { get; set; }

    public int Disposals { get; set; }

    public int Subscriptions { get; set; }

    public List<int> Attempts { get; } = [];

    public int GroupsOpened { get; set; }

    public int GroupsReleased { get; set; }

    public List<List<StateSnapshot>> StateRuns { get; } = [];

    public StateSnapshot? AtAdvance { get; set; }
}

public sealed class ScopedService;

public sealed class Unregistered;

// A synchronous A -> A loop: every Ping it sees emits another Ping (SPEC §14, §6.5). Safety cap: without the depth
// guard a test fails instead of hanging.
public sealed class PingLoopEffect : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.OfActionType<Ping>().Take(300).Select(static object (_) => new Ping());
}

// The debounced search recipe (SPEC §14).
public sealed class DebouncedSearchEffect(TimeProvider time) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.OfActionType<Search>().Debounce(TimeSpan.FromMilliseconds(300), time).Select(static object (s) => new Searched(s.Term));
}

// Records the first snapshot the state observable replays, as `state.Select(s => s.Get<X>())` would read it.
public sealed class FirstStateEffect(Probe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        state.Take(1).Do(probe.Seen.Add).Where(static _ => false).Select(static object (s) => s);
}

// A timer-driven emission: one Tick per second, counted before dispatch, and its disposal counted.
public sealed class IntervalEffect(TimeProvider time, Probe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Interval(TimeSpan.FromSeconds(1), time)
            .Do(onNext: _ => probe.Emissions++, onDispose: () => probe.Disposals++)
            .Select(static object (_) => new Tick());
}

// A pipeline whose teardown throws: user code the host must isolate from the other pipelines' teardown (INV-27).
public sealed class ThrowOnUnsubscribeEffect : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.Do(onDispose: static () => throw new InvalidOperationException("unsubscribe")).Where(static _ => false);
}

// A load with a request in flight for an hour.
public sealed class PendingLoadEffect(TimeProvider time) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.OfActionType<Load>().SelectMany(_ => Observable.Timer(TimeSpan.FromHours(1), time)).Select(static object (_) => new Loaded());
}

public sealed class ScopedDependencyEffect : ReactiveEffect
{
    public ScopedDependencyEffect(ScopedService dependency, Probe probe) => probe.Seen.Add(dependency);

    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Empty<object>();
}

public sealed class AsyncDisposableEffect(Probe probe) : ReactiveEffect, IAsyncDisposable
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Empty<object>();

    public ValueTask DisposeAsync()
    {
        probe.Seen.Add(this);
        return default;
    }
}

public sealed class SyncDisposableEffect(Probe probe) : ReactiveEffect, IDisposable
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Empty<object>();

    public void Dispose() => probe.Seen.Add(this);
}

public sealed class ThrowOnDisposeEffect(Probe probe) : ReactiveEffect, IDisposable
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Empty<object>();

    public void Dispose()
    {
        probe.Seen.Add(this);
        throw new InvalidOperationException("dispose");
    }
}

// A constructor that throws at runtime, which DUCKY309 cannot see: it surfaces at materialization (§6.6).
public sealed class ThrowingCtorEffect : ReactiveEffect
{
    public ThrowingCtorEffect() => throw new InvalidOperationException("ctor");

    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Empty<object>();
}

// A pipeline that fails while being built: user code throwing synchronously from Handle.
public sealed class ThrowingHandleEffect : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        throw new InvalidOperationException("handle");
}

// Records StoreInitialized and Ping into the probe, so a test can tell it from RecordingEffect.
public sealed class LateRecordingEffect(Probe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Merge(actions.OfActionType<StoreInitialized>().Select(static object (a) => a), actions.OfActionType<Ping>().Select(static object (a) => a))
            .Do(probe.Seen.Add)
            .Where(static _ => false);
}

// A slice whose StoreInitialized reducer throws: StoreInitialized commits nothing (SPEC §6.4, INV-13).
public sealed record InitTrap;

public sealed class ThrowOnInitSlice : Slice<InitTrap>
{
    public ThrowOnInitSlice() => On<StoreInitialized>(static _ => throw new InvalidOperationException("init"));

    protected override InitTrap Initial => new();
}

// A core load effect on StoreInitialized, next to ReactiveHost.
public sealed class LoadOnInitEffect : Effect<StoreInitialized>
{
    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken) =>
        context.DispatchAsync(new Loaded());
}

#pragma warning disable CS9113 // justification: the parameter exists only to be unresolvable (DUCKY309)
public sealed class MissingDependencyEffect(Unregistered dependency) : ReactiveEffect
#pragma warning restore CS9113
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Empty<object>();
}

// Boom completes the pipeline with a failure, Hiccup is a non-terminal error, Echo emits a Ping. Every Handle call (a
// subscription) and every RetryDelay attempt is recorded; the third attempt in a row stops resubscribing.
public sealed class FlakyEffect(Probe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state)
    {
        probe.Subscriptions++;
        return Observable.Merge(
            actions.OfActionType<Boom>().Select(static object (_) => throw new InvalidOperationException("boom")).OnErrorResumeAsFailure(),
            actions.OfActionType<Hiccup>().Select(static object (_) => throw new InvalidOperationException("hiccup")),
            actions.OfActionType<Echo>().Select(static object (_) => new Ping()));
    }

    protected internal override TimeSpan? RetryDelay(int attempt)
    {
        probe.Attempts.Add(attempt);
        return attempt < 3 ? base.RetryDelay(attempt) : null;
    }
}

// R3 1.3 ships no GroupBy or GroupByUntil, so the keyed-switch recipe (SPEC §14) carries this operator: a key's group
// completes, and is released, when its duration observable emits; the key's next item opens a new group; the source's
// completion (or failure) completes every open group. One gate serializes the source (the drainer) and the durations
// (timer threads), observer calls included, so a group never completes while an item is pushed into it. As in Rx.NET,
// the duration is subscribed before the group's first item (a Debounce duration must see it), so a duration that emits
// on subscribe closes the group before that item, which is then dropped.
public static class GroupByUntilRecipe
{
    public static Observable<Observable<T>> GroupByUntil<T, TKey>(this Observable<T> source, Func<T, TKey> keySelector, Func<Observable<T>, Observable<Unit>> durationSelector)
        where TKey : notnull =>
        Observable.Create<Observable<T>>(observer =>
        {
            var gate = new object();
            var groups = new Dictionary<TKey, (Subject<T> Items, SingleAssignmentDisposable Duration)>();
            var subscription = source.Subscribe(item =>
            {
                lock (gate)
                {
                    var key = keySelector(item);
                    if (!groups.TryGetValue(key, out var group))
                    {
                        group = (new Subject<T>(), new SingleAssignmentDisposable());
                        groups.Add(key, group);
                        observer.OnNext(group.Items);
                        group.Duration.Disposable = durationSelector(group.Items).Take(1).Subscribe(_ =>
                        {
                            lock (gate)
                            {
                                groups.Remove(key);
                                group.Items.OnCompleted();
                            }
                        });
                    }

                    group.Items.OnNext(item);
                }
            }, observer.OnErrorResume, result =>
            {
                lock (gate)
                {
                    foreach (var group in groups.Values)
                    {
                        group.Duration.Dispose();
                        group.Items.OnCompleted(result);
                    }

                    groups.Clear();
                    observer.OnCompleted(result);
                }
            });
            return Disposable.Create(() =>
            {
                subscription.Dispose();
                lock (gate)
                {
                    foreach (var group in groups.Values)
                    {
                        group.Duration.Dispose();
                    }

                    groups.Clear();
                }
            });
        });
}

// The keyed switch: per item id, a newer LoadItem cancels the load in flight; each id's group is released after ten
// idle seconds, so the pipeline holds no state for ids it no longer sees.
public sealed class KeyedLoadEffect(TimeProvider time, Probe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.OfActionType<LoadItem>()
            .GroupByUntil(static a => a.Id, group => group.Debounce(TimeSpan.FromSeconds(10), time).AsUnitObservable())
            .SelectMany(group =>
            {
                probe.GroupsOpened++;
                return group
                    .Do(onCompleted: _ => probe.GroupsReleased++)
                    .Select(a => Observable.Timer(TimeSpan.FromSeconds(1), time).Select(_ => a.Id))
                    .Switch();
            })
            .Select(static object (id) => new ItemLoaded(id));
}

// Throws on every failure action it sees (INV-12: those failures are logged only, never a loop).
public sealed class ThrowOnFailureEffect : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.Where(static a => a is EffectFailed or ReducerFailed).Select(static object (_) => throw new InvalidOperationException("again"));
}

// Completes successfully as soon as it is subscribed; each subscription is recorded.
public sealed class CompletingEffect(Probe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state)
    {
        probe.Subscriptions++;
        return Observable.Empty<object>();
    }
}

// Handle always throws; RetryDelay is the injected function, so a test picks a throwing, zero or out-of-range delay.
public sealed class RetryingEffect(Probe probe, Func<int, TimeSpan?> retry) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state)
    {
        probe.Subscriptions++;
        throw new InvalidOperationException("handle");
    }

    protected internal override TimeSpan? RetryDelay(int attempt) => retry(attempt);
}

// Vetoes every EffectFailed it is offered: a System failure action is never offered (SPEC §14).
public sealed class VetoEffectFailedMiddleware : Middleware
{
    public override bool MayDispatch(ActionContext context) => context.Action is not EffectFailed;
}

// Throws on every state push once a failure was reduced: the push runs under the failure action (INV-12).
public sealed class ThrowOnFailureStateEffect : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        state.Where(static s => s.Get<IReadOnlyList<EffectFailed>>().Count > 0).Select(static object (_) => throw new InvalidOperationException("state"));
}

// Maps a failure to a Ping and throws on the Ping: a failure under a synchronous descendant of a failure action (INV-12).
public sealed class ThrowOnFailureDescendantEffect : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Merge(
            actions.OfActionType<EffectFailed>().Select(static object (_) => new Ping()),
            actions.OfActionType<Ping>().Select(static object (_) => throw new InvalidOperationException("ping")));
}

// A failure action that is neither EffectFailed nor ReducerFailed, as PersistenceFailed is: Alarm makes the middleware send it.
public sealed record Alarm;

public sealed record SystemFailure;

public sealed class SystemFailureMiddleware : Middleware
{
    public override void AfterReduce(ActionContext context)
    {
        if (context.Action is Alarm)
        {
            DispatchSystem(new SystemFailure(), isFailure: true);
        }
    }
}

public sealed class ThrowOnSystemFailureEffect : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.OfActionType<SystemFailure>().Select(static object (_) => throw new InvalidOperationException("system"));
}

// Throws a second after every failure the injected filter lets through: an asynchronous hop, so a new chain (§14, ADR-0026).
public sealed class DelayedThrowOnFailureEffect(TimeProvider time, Func<EffectFailed, bool> filter) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.OfActionType<EffectFailed>().Where(filter).Delay(TimeSpan.FromSeconds(1), time).Select(static object (_) => throw new InvalidOperationException("late"));
}

// A real run's context and the store's bridge, so a test can drive one ReactivePipeline over a composite it can count.
internal sealed class PipelineSeam
{
    public ReactiveBridge Bridge { get; set; } = null!;

    public EffectContext Context { get; set; } = null!;
}

internal sealed class PipelineSeamEffect(PipelineSeam seam, ReactiveBridge bridge) : Effect<StoreInitialized>
{
    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        seam.Bridge = bridge;
        seam.Context = context;
        return Task.CompletedTask;
    }
}

// A real store from DI, scoped like a server circuit, on a FakeTimeProvider.
internal sealed class Host : IAsyncDisposable
{
    private static readonly ServiceProviderOptions _validating = new() { ValidateScopes = true, ValidateOnBuild = true };
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    public Host(Action<DuckyBuilder> configure, Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection().AddSingleton<TimeProvider>(Time).AddSingleton(Probe);
        services?.Invoke(collection);
        collection.AddDucky(configure);
        _provider = collection.BuildServiceProvider(_validating);
        _scope = _provider.CreateAsyncScope();
        Store = _scope.ServiceProvider.GetRequiredService<IStore>();
    }

    public FakeTimeProvider Time { get; } = new();

    public Probe Probe { get; } = new();

    public IStore Store { get; }

    // A host that blocked quiescence (or init) would hang the suite: every wait is bounded, so it fails with a
    // TimeoutException instead.
    public static readonly TimeSpan HangBound = TimeSpan.FromSeconds(10);

    public async Task StartAsync()
    {
        await Store.InitializeAsync(TestContext.Current.CancellationToken).WaitAsync(HangBound, TestContext.Current.CancellationToken);
        await IdleAsync();
    }

    public Task IdleAsync() =>
        Store.WhenIdleAsync(TestContext.Current.CancellationToken).WaitAsync(HangBound, TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}

// Records every StoreInitialized it sees; Boom completes it with a failure, so it resubscribes after a second (SPEC §14).
public sealed class InitThenFlakyEffect(Probe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state)
    {
        probe.Subscriptions++;
        return Observable.Merge(
            actions.OfActionType<StoreInitialized>().Do(probe.Seen.Add).Where(static _ => false).Select(static object (a) => a),
            actions.OfActionType<Boom>().Select(static object (_) => throw new InvalidOperationException("boom")).OnErrorResumeAsFailure());
    }
}

// Each subscription records every state value it sees in its own list; Boom completes it with a failure.
public sealed class StateRunsFlakyEffect(Probe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state)
    {
        probe.Subscriptions++;
        List<StateSnapshot> run = [];
        probe.StateRuns.Add(run);
        return Observable.Merge(
            state.Do(run.Add).Where(static _ => false).Select(static object (s) => s),
            actions.OfActionType<Boom>().Select(static object (_) => throw new InvalidOperationException("boom")).OnErrorResumeAsFailure());
    }
}

// A long-lived subscriber of both streams, in the order it receives them: it must never be lost to a resubscribe.
public sealed class WitnessEffect(Probe probe) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Merge(actions, state.Select(static object (s) => s)).Do(probe.Seen.Add).Where(static _ => false);
}

// Registered before ReactiveMiddleware: on a Ping it fires the due timers from inside AfterReduce, after the commit and
// before the bridge's State.OnNext write (SPEC §14), recording the store's state at that moment.
public sealed class AdvanceOnPingMiddleware(TimeProvider time, Probe probe) : Middleware
{
    public override void AfterReduce(ActionContext context)
    {
        if (context.Action is Ping)
        {
            probe.AtAdvance = context.State;
            ((FakeTimeProvider)time).Advance(TimeSpan.FromSeconds(1));
        }
    }
}
