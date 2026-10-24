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
    }

    protected override Counter Initial => new(0, null, 0, false);
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

#pragma warning disable CS9113 // justification: the parameter exists only to be unresolvable (DUCKY309)
public sealed class MissingDependencyEffect(Unregistered dependency) : ReactiveEffect
#pragma warning restore CS9113
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        Observable.Empty<object>();
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
