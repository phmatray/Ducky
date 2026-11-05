using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using R3;

namespace Ducky.Reactive.Tests;

// SPEC §14 (ReactiveEffect, AddReactiveEffect, ReactiveBridge, ReactiveMiddleware, ReactiveHost); INV-27.
public sealed class ReactiveHostTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reactive_StateObservable_FirstValueIsCurrentSnapshot()
    {
        // No persistence, no seed: nothing changes state before StoreInitialized, and ReactiveMiddleware writes the property
        // only on a change, so only the host's write gives it a value; without it the pipeline would replay null.
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<FirstStateEffect>());

        await host.StartAsync();

        host.Probe.Seen.ShouldHaveSingleItem().ShouldNotBeNull().ShouldBeSameAs(host.Store.State);
    }

    [Fact]
    public async Task Reactive_SyncSelfLoop_StoppedByDepthGuard()
    {
        // Each emission happens inside AfterReduce, so it is a synchronous child: depths 0..64 are reduced and the Ping at
        // depth 65 is dropped with a ReducerFailed(DispatchLoopException).
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddSlice<FailureSlice>().AddReactiveEffect<PingLoopEffect>());
        await host.StartAsync();

        (await host.Store.DispatchAsync(new Ping())).ShouldBe(DispatchResult.Reduced);
        await host.IdleAsync();

        host.Store.State.Get<Counter>().Pings.ShouldBe(65);
        host.Store.State.Get<IReadOnlyList<ReducerFailed>>().ShouldHaveSingleItem().Exception.ShouldBeOfType<DispatchLoopException>();
    }

    [Fact]
    public async Task Reactive_DoesNotBlockWhenIdle()
    {
        // A pipeline with a request in flight is not counted: WhenIdleAsync completes at once, and the load still lands.
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<PendingLoadEffect>());
        await host.StartAsync();

        (await host.Store.DispatchAsync(new Load())).ShouldBe(DispatchResult.Reduced);

        host.Store.WhenIdleAsync(Ct).IsCompletedSuccessfully.ShouldBeTrue();
        host.Store.State.Get<Counter>().Loaded.ShouldBeFalse();
        host.Time.Advance(TimeSpan.FromHours(1));
        await host.IdleAsync();
        host.Store.State.Get<Counter>().Loaded.ShouldBeTrue();
    }

    [Fact]
    public async Task Reactive_Debounce_WithFakeTime()
    {
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<DebouncedSearchEffect>());
        await host.StartAsync();

        await host.Store.DispatchAsync(new Search("a"));
        host.Time.Advance(TimeSpan.FromMilliseconds(100));
        await host.Store.DispatchAsync(new Search("ab"));
        await host.Store.DispatchAsync(new Search("abc"));
        host.Time.Advance(TimeSpan.FromMilliseconds(299));
        await host.IdleAsync();
        host.Store.State.Get<Counter>().Searched.ShouldBeNull();

        host.Time.Advance(TimeSpan.FromMilliseconds(1));
        await host.IdleAsync();
        host.Store.State.Get<Counter>().Searched.ShouldBe("abc");
    }

    [Fact]
    public async Task Reactive_DisposedWithStore_NoEmissionAfterDispose()
    {
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<IntervalEffect>());
        await host.StartAsync();
        host.Time.Advance(TimeSpan.FromSeconds(2));
        await host.IdleAsync();
        host.Store.State.Get<Counter>().Ticks.ShouldBe(2);

        await host.Store.DisposeAsync();
        host.Time.Advance(TimeSpan.FromSeconds(5));

        host.Probe.Disposals.ShouldBe(1);
        host.Probe.Emissions.ShouldBe(2);
        host.Store.State.Get<Counter>().Ticks.ShouldBe(2);
    }

    // Non-normative (INV-27): a pipeline whose teardown throws is logged (Error 3002, naming the effect) and never leaves a
    // later pipeline subscribed after the store is disposed.
    [Fact]
    public async Task Reactive_PipelineTeardownThrows_OthersStillDisposedAndLogged()
    {
        var fake = new FakeLogger<ReactiveHost>();
        await using var host = new Host(
            d => d.AddSlice<CounterSlice>().AddReactiveEffect<ThrowOnUnsubscribeEffect>().AddReactiveEffect<IntervalEffect>(),
            s => s.AddSingleton<ILogger<ReactiveHost>>(fake));
        await host.StartAsync();
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.IdleAsync();

        await host.Store.DisposeAsync();
        host.Time.Advance(TimeSpan.FromSeconds(5));

        host.Probe.Disposals.ShouldBe(1);
        host.Probe.Emissions.ShouldBe(1);
        var record = fake.Collector.GetSnapshot().Where(static r => r.Id.Id != 3000).ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(3002);
        record.Level.ShouldBe(LogLevel.Error);
        record.Message.ShouldBe($"Unsubscribing reactive effect {typeof(ThrowOnUnsubscribeEffect)} threw");
        record.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("unsubscribe");
    }

    // Non-normative (§14, INV-27): a reactive effect whose Handle (or its Subscribe) throws is a reactive failure: logged
    // (Error 3003), published as EffectFailed(type, "reactive", ex) and retried after RetryDelay. The pipelines before and
    // after it still start and still see StoreInitialized.
    [Fact]
    public async Task Reactive_HandleThrows_OtherPipelinesStillStartAndSeeStoreInitialized()
    {
        var seen = new List<object>();
        var fake = new FakeLogger<ReactiveHost>();
        await using var host = new Host(
            d => d.AddSlice<CounterSlice>().AddSlice<EffectFailureSlice>().AddReactiveEffect<RecordingEffect>().AddReactiveEffect<ThrowingHandleEffect>().AddReactiveEffect<LateRecordingEffect>(),
            s => s.AddSingleton(seen).AddSingleton<ILogger<ReactiveHost>>(fake));
        await host.StartAsync();

        await host.Store.DispatchAsync(new Ping());

        seen.ShouldBe([new StoreInitialized(), new Ping()]);
        host.Probe.Seen.ShouldBe([new StoreInitialized(), new Ping()]);
        var record = fake.Collector.GetSnapshot().Where(static r => r.Id.Id != 3000).ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(3003);
        record.Level.ShouldBe(LogLevel.Error);
        record.Message.ShouldBe($"Reactive effect {typeof(ThrowingHandleEffect)} failed");
        record.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("handle");
        var failure = host.Store.State.Get<IReadOnlyList<EffectFailed>>().ShouldHaveSingleItem();
        (failure.EffectType, failure.ActionType, failure.Exception.Message).ShouldBe((typeof(ThrowingHandleEffect).ToString(), "reactive", "handle"));

        // Attempt 1 waits one second, then Handle runs (and throws) again.
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.IdleAsync();
        fake.Collector.GetSnapshot().Count(static r => r.Id.Id == 3003).ShouldBe(2);
        host.Store.State.Get<IReadOnlyList<EffectFailed>>().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Reactive_ScopedDependencyInBrowser_Resolves()
    {
        // The browser lifetime (Singleton): the store is resolved from the root and creates reactive effects from its own
        // store scope, so a scoped dependency resolves under scope validation. IsBrowser is internal to Ducky (no IVT for
        // this project), so the test sets the lifetime it selects.
        var probe = new Probe();
        var services = new ServiceCollection().AddSingleton(probe).AddScoped<ScopedService>();
        services.AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Singleton;
            d.AddSlice<CounterSlice>().AddReactiveEffect<ScopedDependencyEffect>();
        });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var store = provider.GetRequiredService<IStore>();
        await store.InitializeAsync(Ct).WaitAsync(Host.HangBound, Ct);

        probe.Seen.ShouldHaveSingleItem().ShouldBeOfType<ScopedService>();
    }

    // Non-normative (§5.6, §6.11): the store owns and disposes reactive effects, IAsyncDisposable and IDisposable alike,
    // in reverse construction order.
    [Fact]
    public async Task Reactive_DisposableEffects_DisposedWithStore()
    {
        await using var host = new Host(d => d.AddReactiveEffect<AsyncDisposableEffect>().AddReactiveEffect<SyncDisposableEffect>().AddReactiveEffect<PingLoopEffect>());
        await host.StartAsync();
        host.Probe.Seen.ShouldBeEmpty();

        await host.Store.DisposeAsync();

        host.Probe.Seen.Select(static e => e.GetType()).ShouldBe([typeof(SyncDisposableEffect), typeof(AsyncDisposableEffect)]);
    }

    // Non-normative (§6.11 5b, INV-29): a throwing Dispose is logged (Error 3001, naming the effect) and never stops the
    // disposal of the others.
    [Fact]
    public async Task Reactive_EffectDisposeThrows_OthersStillDisposedAndLogged()
    {
        var fake = new FakeLogger<ReactiveHost>();
        await using var host = new Host(
            d => d.AddReactiveEffect<SyncDisposableEffect>().AddReactiveEffect<ThrowOnDisposeEffect>().AddReactiveEffect<AsyncDisposableEffect>(),
            s => s.AddSingleton<ILogger<ReactiveHost>>(fake));
        await host.StartAsync();

        await host.Store.DisposeAsync();

        host.Probe.Seen.Select(static e => e.GetType()).ShouldBe([typeof(AsyncDisposableEffect), typeof(ThrowOnDisposeEffect), typeof(SyncDisposableEffect)]);
        AssertDisposeThrewLogged(fake);
    }

    // Non-normative (§6.6, INV-31): the core never sees reactive effects, so when a later one's constructor throws, the
    // host disposes the ones it already built, in reverse order and each isolated, before the store reports DUCKY353.
    [Fact]
    public async Task Reactive_EffectCtorThrows_EarlierEffectsDisposed()
    {
        var fake = new FakeLogger<ReactiveHost>();
        await using var host = new Host(
            d => d.AddReactiveEffect<SyncDisposableEffect>().AddReactiveEffect<ThrowOnDisposeEffect>().AddReactiveEffect<AsyncDisposableEffect>().AddReactiveEffect<ThrowingCtorEffect>(),
            s => s.AddSingleton<ILogger<ReactiveHost>>(fake));

        var thrown = Should.Throw<DuckyConfigurationException>(() => host.Store.Dispatch(new Ping()));

        thrown.Errors.ShouldHaveSingleItem().Code.ShouldBe("DUCKY353");
        host.Probe.Seen.Select(static e => e.GetType()).ShouldBe([typeof(AsyncDisposableEffect), typeof(ThrowOnDisposeEffect), typeof(SyncDisposableEffect)]);
        AssertDisposeThrewLogged(fake);
        await host.Store.DisposeAsync();
        host.Probe.Seen.Count.ShouldBe(3);
    }

    // Non-normative (§14): pipelines are subscribed once. A user-dispatched StoreInitialized reaches them as an ordinary
    // action, once, and subscribes nothing more.
    [Fact]
    public async Task Reactive_StoreInitializedDispatchedAgain_PipelinesSubscribedOnce()
    {
        var seen = new List<object>();
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<RecordingEffect>(), s => s.AddSingleton(seen));
        await host.StartAsync();

        await host.Store.DispatchAsync(new StoreInitialized());
        await host.Store.DispatchAsync(new Ping());

        seen.ShouldBe([new StoreInitialized(), new StoreInitialized(), new Ping()]);
    }

    // Non-normative (§14): every action reaching AfterReduce after StoreInitialized is pushed, StoreInitialized once,
    // matched by exact type only (ADR-0008).
    [Fact]
    public async Task Reactive_Actions_StoreInitializedOnceThenEveryAction()
    {
        var seen = new List<object>();
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<RecordingEffect>(), s => s.AddSingleton(seen));
        await host.StartAsync();

        await host.Store.DispatchAsync(new Ping());
        await host.Store.DispatchAsync("unrelated");
        await host.Store.DispatchAsync(new DerivedAction());
        await host.Store.DispatchAsync(new BaseAction());

        seen.ShouldBe([new StoreInitialized(), new Ping(), new BaseAction()]);
    }

    // Non-normative (§14): the state observable replays the current snapshot, then emits each change once.
    [Fact]
    public async Task Reactive_StateObservable_EmitsEachChange()
    {
        var seen = new List<object>();
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<PingCountEffect>(), s => s.AddSingleton(seen));
        await host.StartAsync();

        await host.Store.DispatchAsync(new Ping());
        await host.Store.DispatchAsync("unchanged");
        await host.Store.DispatchAsync(new Ping());

        seen.ShouldBe([0, 1, 2]);
    }

    // Non-normative (§14): an action pipeline that reads state sees the snapshot after that action was reduced, as
    // ReactiveHost does for StoreInitialized (WithLatestFrom, CombineLatest, SelectMany(_ => state.Take(1))).
    [Fact]
    public async Task Reactive_ActionObservers_SeeStateAfterThatAction()
    {
        var seen = new List<object>();
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<PingStateEffect>(), s => s.AddSingleton(seen));
        await host.StartAsync();

        await host.Store.DispatchAsync(new Ping());
        await host.Store.DispatchAsync(new Ping());

        seen.ShouldBe([1, 2]);
    }

    // Non-normative (§5.1, like AddEffect<T>): registering a reactive effect twice, as a generated registration plus a
    // manual call would, creates one instance and subscribes one pipeline.
    [Fact]
    public async Task AddReactiveEffect_Twice_OnePipeline()
    {
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<FirstStateEffect>().AddReactiveEffect<FirstStateEffect>());

        await host.StartAsync();

        host.Probe.Seen.ShouldHaveSingleItem();
    }

    // Non-normative: AddReactiveEffect validates its builder argument.
    [Fact]
    public void AddReactiveEffect_NullBuilder_Throws() =>
        Should.Throw<ArgumentNullException>(() => DuckyReactiveExtensions.AddReactiveEffect<PingLoopEffect>(null!));

    // Non-normative (§14): the default backoff doubles from one second, caps at 30 s, and stops after attempt 8.
    [Fact]
    public void RetryDelay_Default_DoublesCapsAndStops()
    {
        var effect = new PingLoopEffect();

        TimeSpan? Seconds(int s) => TimeSpan.FromSeconds(s);
        Enumerable.Range(1, 9).Select(effect.RetryDelay).ShouldBe(
            [Seconds(1), Seconds(2), Seconds(4), Seconds(8), Seconds(16), Seconds(30), Seconds(30), Seconds(30), null]);
    }

    private static void AssertDisposeThrewLogged(FakeLogger<ReactiveHost> fake)
    {
        var record = fake.Collector.GetSnapshot().Where(static r => r.Id.Id != 3000).ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(3001);
        record.Level.ShouldBe(LogLevel.Error);
        record.Message.ShouldBe($"Disposing reactive effect {typeof(ThrowOnDisposeEffect)} threw");
        record.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("dispose");
    }

    public sealed class PingCountEffect(List<object> seen) : ReactiveEffect
    {
        public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
            state.Select(static object (s) => s.Get<Counter>().Pings).Do(seen.Add).Where(static _ => false);
    }

    public sealed class PingStateEffect(List<object> seen) : ReactiveEffect
    {
        public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
            actions.OfActionType<Ping>().WithLatestFrom(state, static object (_, s) => s.Get<Counter>().Pings).Do(seen.Add).Where(static _ => false);
    }

    public sealed class RecordingEffect(List<object> seen) : ReactiveEffect
    {
        public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
            Observable.Merge(
                    actions.OfActionType<StoreInitialized>().Select(static object (a) => a),
                    actions.OfActionType<Ping>().Select(static object (a) => a),
                    actions.OfActionType<BaseAction>().Select(static object (a) => a))
                .Do(seen.Add)
                .Where(static _ => false);
    }
}
