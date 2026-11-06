using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using R3;

namespace Ducky.Reactive.Tests;

// SPEC §14 (failures, resubscribe, the GroupByUntil recipe); INV-27, INV-12.
public sealed class ReactiveFailureTests
{
    private static readonly string _flaky = typeof(FlakyEffect).ToString();

    private static Host FlakyHost(FakeLogger<ReactiveHost>? logger = null) =>
        new(
            d => d.AddSlice<CounterSlice>().AddSlice<EffectFailureSlice>().AddReactiveEffect<FlakyEffect>(),
            s => s.AddSingleton<ILogger<ReactiveHost>>(logger ?? new FakeLogger<ReactiveHost>()));

    private static Host RetryingHost(Func<int, TimeSpan?> retry, FakeLogger<ReactiveHost>? logger = null, Action<DuckyBuilder>? more = null) =>
        new(
            d =>
            {
                d.AddSlice<CounterSlice>().AddSlice<EffectFailureSlice>().AddReactiveEffect<RetryingEffect>();
                more?.Invoke(d);
            },
            s => s.AddSingleton(retry).AddSingleton<ILogger<ReactiveHost>>(logger ?? new FakeLogger<ReactiveHost>()));

    private static IEnumerable<(int Id, string? Message)> Records(FakeLogger<ReactiveHost> logger) =>
        logger.Collector.GetSnapshot().Where(static r => r.Id.Id != 3000).Select(static r => (r.Id.Id, r.Exception?.Message));

    private static async Task DispatchIdleAsync(Host host, object action)
    {
        await host.Store.DispatchAsync(action);
        await host.IdleAsync();
    }

    [Fact]
    public async Task Reactive_FailureCompletion_ResubscribesWithBackoff()
    {
        await using var host = FlakyHost();
        await host.StartAsync();
        host.Probe.Subscriptions.ShouldBe(1);

        await DispatchIdleAsync(host, new Boom());
        var failure = host.Store.State.Get<IReadOnlyList<EffectFailed>>().ShouldHaveSingleItem();
        failure.EffectType.ShouldBe(_flaky);
        failure.ActionType.ShouldBe("reactive");
        failure.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom");

        // Attempt 1 waits one second: the dead pipeline sees nothing meanwhile.
        host.Time.Advance(TimeSpan.FromMilliseconds(999));
        await DispatchIdleAsync(host, new Echo());
        host.Probe.Subscriptions.ShouldBe(1);
        host.Store.State.Get<Counter>().Pings.ShouldBe(0);
        host.Time.Advance(TimeSpan.FromMilliseconds(1));
        host.Probe.Subscriptions.ShouldBe(2);

        // Attempt 2 waits two seconds.
        await DispatchIdleAsync(host, new Boom());
        host.Time.Advance(TimeSpan.FromMilliseconds(1999));
        host.Probe.Subscriptions.ShouldBe(2);
        host.Time.Advance(TimeSpan.FromMilliseconds(1));
        host.Probe.Subscriptions.ShouldBe(3);

        // An emission resets the counter: the next failure is attempt 1 again, one second.
        await DispatchIdleAsync(host, new Echo());
        host.Store.State.Get<Counter>().Pings.ShouldBe(1);
        await DispatchIdleAsync(host, new Boom());
        host.Time.Advance(TimeSpan.FromSeconds(1));
        host.Probe.Subscriptions.ShouldBe(4);

        host.Probe.Attempts.ShouldBe([1, 2, 1]);
        host.Store.State.Get<IReadOnlyList<EffectFailed>>().Count.ShouldBe(3);
    }

    [Fact]
    public async Task Reactive_RetryDelayNull_StopsResubscribingWithWarning()
    {
        var logger = new FakeLogger<ReactiveHost>();
        await using var host = FlakyHost(logger);
        await host.StartAsync();

        await DispatchIdleAsync(host, new Boom());
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await DispatchIdleAsync(host, new Boom());
        host.Time.Advance(TimeSpan.FromSeconds(2));
        await DispatchIdleAsync(host, new Boom());
        host.Time.Advance(TimeSpan.FromDays(1));

        host.Probe.Attempts.ShouldBe([1, 2, 3]);
        host.Probe.Subscriptions.ShouldBe(3);
        host.Store.State.Get<IReadOnlyList<EffectFailed>>().Count.ShouldBe(3);
        var warning = logger.Collector.GetSnapshot().Where(static r => r.Id.Id == 3004).ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldBe($"Reactive effect {_flaky} stopped resubscribing after 3 failures");
    }

    [Fact]
    public async Task Reactive_OnErrorResume_ContinuesStream()
    {
        var logger = new FakeLogger<ReactiveHost>();
        await using var host = FlakyHost(logger);
        await host.StartAsync();

        await DispatchIdleAsync(host, new Hiccup());
        await DispatchIdleAsync(host, new Hiccup());
        await DispatchIdleAsync(host, new Echo());
        host.Time.Advance(TimeSpan.FromDays(1));

        host.Store.State.Get<IReadOnlyList<EffectFailed>>().Select(static f => f.Exception.Message).ShouldBe(["hiccup", "hiccup"]);
        host.Store.State.Get<Counter>().Pings.ShouldBe(1);
        host.Probe.Subscriptions.ShouldBe(1);
        host.Probe.Attempts.ShouldBeEmpty();
        var errors = logger.Collector.GetSnapshot().Where(static r => r.Id.Id == 3003).ToList();
        errors.Count.ShouldBe(2);
        errors[0].Level.ShouldBe(LogLevel.Error);
        errors[0].Message.ShouldBe($"Reactive effect {_flaky} failed");
        errors[0].Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("hiccup");
    }

    [Fact]
    public async Task Reactive_Failure_IsSystemFailureAction_StrictModeClean()
    {
        // Strict: an unhandled action would be a ReducerFailed. A middleware vetoes every EffectFailed it is offered, and a
        // reducer throws on each one, yet both reactive failures are reduced, as System actions, and are the only failures:
        // the reducer's failures are raised under a failure action, so they are logged, never dispatched (INV-12).
        var probe = new Probe();
        await using var store = TestStore.Create(
            d => d.AddSlice<CounterSlice>().AddSlice<ThrowOnEffectFailedSlice>().Use<VetoEffectFailedMiddleware>().AddReactiveEffect<FlakyEffect>(),
            s => s.AddSingleton(probe));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        (await store.DispatchAsync(new Hiccup())).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new Boom())).ShouldBe(DispatchResult.Reduced);
        await store.Settled();

        store.Failures.Cast<EffectFailed>().Select(static f => (f.EffectType, f.ActionType, f.Exception.Message))
            .ShouldBe([(_flaky, "reactive", "hiccup"), (_flaky, "reactive", "boom")]);
        store.Processed.Where(static c => c.Action is EffectFailed).Select(static c => c.Origin).ShouldBe([Origin.System, Origin.System]);

        // No reducer for EffectFailed at all: a strict store still flags nothing. EffectFailed is a failure action, so an
        // UnhandledActionException under it would be logged (INV-12) rather than dispatched: the log is the witness.
        var logs = new FakeLoggerProvider();
        await using var bare = TestStore.Create(
            d => d.AddSlice<CounterSlice>().AddReactiveEffect<FlakyEffect>(),
            s => s.AddSingleton(new Probe()).AddLogging(b => b.AddProvider(logs)));
        await bare.InitializeAsync(TestContext.Current.CancellationToken);

        await bare.DispatchAsync(new Hiccup());
        await bare.Settled();

        bare.Failures.ShouldHaveSingleItem().ShouldBeOfType<EffectFailed>().ActionType.ShouldBe("reactive");
        logs.Collector.GetSnapshot().ShouldNotContain(static r => r.Exception is UnhandledActionException);
    }

    // Non-normative (INV-12, §14): a reactive failure raised while handling a failure action is logged (Error 3003), never
    // dispatched, so a pipeline that throws on every failure can't loop. The next failure outside one is dispatched again.
    [Fact]
    public async Task Reactive_FailureWhileHandlingFailureAction_LogsOnly()
    {
        var logger = new FakeLogger<ReactiveHost>();
        await using var host = new Host(
            d => d.AddSlice<CounterSlice>().AddSlice<FailureSlice>().AddSlice<EffectFailureSlice>().AddReactiveEffect<FlakyEffect>().AddReactiveEffect<ThrowOnFailureEffect>(),
            s => s.AddSingleton<ILogger<ReactiveHost>>(logger));
        await host.StartAsync();

        await DispatchIdleAsync(host, new Hiccup());
        await DispatchIdleAsync(host, new Crash());
        await DispatchIdleAsync(host, new Hiccup());

        host.Store.State.Get<IReadOnlyList<EffectFailed>>().Select(static f => f.Exception.Message).ShouldBe(["hiccup", "hiccup"]);
        host.Store.State.Get<IReadOnlyList<ReducerFailed>>().ShouldHaveSingleItem().Exception.Message.ShouldBe("crash");
        var again = logger.Collector.GetSnapshot().Where(static r => r.Id.Id == 3003 && r.Exception?.Message == "again").ToList();
        again.Count.ShouldBe(3);
        again.ShouldAllBe(static r => r.Message == $"Reactive effect {typeof(ThrowOnFailureEffect)} failed");
    }

    // Non-normative (INV-12, §14): the state push runs under the failure action too, so a pipeline that throws on a failure's
    // state change is logged only, and the failure can't feed itself.
    [Fact]
    public async Task Reactive_StatePipelineThrowsOnFailureAction_LogsOnly()
    {
        var logger = new FakeLogger<ReactiveHost>();
        await using var host = new Host(
            d => d.AddSlice<CounterSlice>().AddSlice<EffectFailureSlice>().AddReactiveEffect<FlakyEffect>().AddReactiveEffect<ThrowOnFailureStateEffect>(),
            s => s.AddSingleton<ILogger<ReactiveHost>>(logger));
        await host.StartAsync();

        await DispatchIdleAsync(host, new Hiccup());

        host.Store.State.Get<IReadOnlyList<EffectFailed>>().ShouldHaveSingleItem().Exception.Message.ShouldBe("hiccup");
        var swallowed = logger.Collector.GetSnapshot().Where(static r => r.Exception?.Message == "state").ShouldHaveSingleItem();
        swallowed.Id.Id.ShouldBe(3003);
        swallowed.Level.ShouldBe(LogLevel.Error);
        swallowed.Message.ShouldBe($"Reactive effect {typeof(ThrowOnFailureStateEffect)} failed");
    }

    // Non-normative (INV-12): a synchronous descendant of a failure action is covered like the action itself, so a pipeline
    // that maps a failure to an action and throws on that action can't loop up to the depth guard.
    [Fact]
    public async Task Reactive_FailureUnderSynchronousDescendantOfFailure_LogsOnly()
    {
        var logger = new FakeLogger<ReactiveHost>();
        await using var host = new Host(
            d => d.AddSlice<CounterSlice>().AddSlice<EffectFailureSlice>().AddReactiveEffect<FlakyEffect>().AddReactiveEffect<ThrowOnFailureDescendantEffect>(),
            s => s.AddSingleton<ILogger<ReactiveHost>>(logger));
        await host.StartAsync();

        await DispatchIdleAsync(host, new Hiccup());

        host.Store.State.Get<IReadOnlyList<EffectFailed>>().ShouldHaveSingleItem().Exception.Message.ShouldBe("hiccup");
        host.Store.State.Get<Counter>().Pings.ShouldBe(1);
        logger.Collector.GetSnapshot().Where(static r => r.Exception?.Message == "ping").ShouldHaveSingleItem().Id.Id.ShouldBe(3003);
    }

    // Non-normative (INV-12): anything sent with DispatchSystem(isFailure: true) is a failure action, not only the core's.
    [Fact]
    public async Task Reactive_FailureUnderDispatchSystemFailureAction_LogsOnly()
    {
        var logger = new FakeLogger<ReactiveHost>();
        await using var host = new Host(
            d => d.AddSlice<CounterSlice>().AddSlice<EffectFailureSlice>().Use<SystemFailureMiddleware>().AddReactiveEffect<ThrowOnSystemFailureEffect>(),
            s => s.AddSingleton<ILogger<ReactiveHost>>(logger));
        await host.StartAsync();

        await DispatchIdleAsync(host, new Alarm());

        host.Store.State.Get<IReadOnlyList<EffectFailed>>().ShouldBeEmpty();
        logger.Collector.GetSnapshot().Where(static r => r.Exception?.Message == "system").ShouldHaveSingleItem().Id.Id.ShouldBe(3003);
    }

    // Non-normative (§14, ADR-0026): after a real asynchronous hop a failure starts a new chain and is published, so an
    // unfiltered pipeline sustains itself once a second without limit. The documented filter ends it at one publication.
    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 4)]
    public async Task Reactive_AsynchronousPipelineOnEffectFailed_RecursUnlessReactiveFiltered(bool filtered, int expected)
    {
        Func<EffectFailed, bool> filter = filtered ? static f => f.ActionType != "reactive" : static _ => true;
        await using var host = new Host(
            d => d.AddSlice<EffectFailureSlice>().AddReactiveEffect<DelayedThrowOnFailureEffect>(),
            s => s.AddSingleton(filter));
        await host.StartAsync();

        await DispatchIdleAsync(host, new EffectFailed("core", "action", new InvalidOperationException("first")));
        for (var second = 0; second < 3; second++)
        {
            host.Time.Advance(TimeSpan.FromSeconds(1));
            await host.IdleAsync();
        }

        host.Store.State.Get<IReadOnlyList<EffectFailed>>().Count.ShouldBe(expected);
    }

    // Non-normative (§14): an ended subscription leaves the composite when the pipeline resubscribes. An emission resets the
    // attempt, so a pipeline that emits and fails retries forever: the composite must not grow with it.
    [Fact]
    public async Task Reactive_EmitAndFailForever_RetainsOneSubscription()
    {
        var seam = new PipelineSeam();
        await using var host = new Host(
            d => d.AddSlice<CounterSlice>().AddSlice<EffectFailureSlice>().AddEffect<PipelineSeamEffect>().AddReactiveEffect<CompletingEffect>(),
            s => s.AddSingleton(seam));
        await host.StartAsync();
        using var subscriptions = new CompositeDisposable();
        var effect = new FlakyEffect(new Probe());

        new ReactivePipeline(effect, seam.Bridge, seam.Context, subscriptions, new(NullLogger.Instance), TestContext.Current.CancellationToken).Subscribe();
        subscriptions.Count.ShouldBe(1);
        for (var round = 1; round <= 5; round++)
        {
            await DispatchIdleAsync(host, new Boom());
            host.Time.Advance(TimeSpan.FromSeconds(1));
            subscriptions.Count.ShouldBe(1);
            await DispatchIdleAsync(host, new Echo());
            host.Store.State.Get<Counter>().Pings.ShouldBe(round);
        }

        // What it retains is the live one: disposing the composite stops the pipeline.
        subscriptions.Dispose();
        await DispatchIdleAsync(host, new Echo());
        host.Store.State.Get<Counter>().Pings.ShouldBe(5);
    }

    // Non-normative (§14): a Handle that throws leaves no subscription behind either, however many attempts it takes.
    [Fact]
    public async Task Reactive_HandleThrowsOnEveryAttempt_RetainsOneSubscription()
    {
        var seam = new PipelineSeam();
        await using var host = new Host(
            d => d.AddSlice<EffectFailureSlice>().AddEffect<PipelineSeamEffect>().AddReactiveEffect<CompletingEffect>(),
            s => s.AddSingleton(seam));
        await host.StartAsync();
        using var subscriptions = new CompositeDisposable();
        var probe = new Probe();
        var effect = new RetryingEffect(probe, static _ => TimeSpan.Zero);

        new ReactivePipeline(effect, seam.Bridge, seam.Context, subscriptions, new(NullLogger.Instance), TestContext.Current.CancellationToken).Subscribe();
        for (var attempt = 2; attempt <= 5; attempt++)
        {
            host.Time.Advance(TimeSpan.FromMilliseconds(1));
            probe.Subscriptions.ShouldBe(attempt);
            subscriptions.Count.ShouldBe(1);
        }
    }

    // Non-normative (INV-27): a RetryDelay that throws is user code like Handle: logged (Error 3003) and published, it stops
    // resubscribing (Warning 3004) and never stops the host, so a later pipeline still starts and sees StoreInitialized.
    [Fact]
    public async Task Reactive_RetryDelayThrows_LoggedStopsAndOtherPipelinesStart()
    {
        var logger = new FakeLogger<ReactiveHost>();
        await using var host = RetryingHost(static _ => throw new InvalidOperationException("retry"), logger, d => d.AddReactiveEffect<LateRecordingEffect>());
        await host.StartAsync();
        host.Time.Advance(TimeSpan.FromDays(1));

        await host.IdleAsync();

        host.Probe.Seen.ShouldBe([new StoreInitialized()]);
        host.Probe.Subscriptions.ShouldBe(1);
        Records(logger).ShouldBe([(3003, "handle"), (3003, "retry"), (3004, null)]);
        host.Store.State.Get<IReadOnlyList<EffectFailed>>().Select(static f => (f.ActionType, f.Exception.Message)).ShouldBe([("reactive", "handle"), ("reactive", "retry")]);
    }

    // Non-normative (INV-27): a delay Task.Delay rejects (above about 49.7 days) stops resubscribing like null, logged and
    // published.
    [Fact]
    public async Task Reactive_RetryDelayOutOfRange_LoggedAndStops()
    {
        var logger = new FakeLogger<ReactiveHost>();
        await using var host = RetryingHost(static _ => TimeSpan.FromDays(60), logger);
        await host.StartAsync();
        host.Time.Advance(TimeSpan.FromDays(100));

        host.Probe.Subscriptions.ShouldBe(1);
        var records = logger.Collector.GetSnapshot().Where(static r => r.Id.Id != 3000).ToList();
        records.Select(static r => r.Id.Id).ShouldBe([3003, 3003, 3004]);
        records[1].Exception.ShouldBeOfType<ArgumentOutOfRangeException>();
        records[2].Message.ShouldBe($"Reactive effect {typeof(RetryingEffect)} stopped resubscribing after 1 failures");
        await host.IdleAsync();
        var failures = host.Store.State.Get<IReadOnlyList<EffectFailed>>();
        failures.Select(static f => f.ActionType).ShouldBe(["reactive", "reactive"]);
        failures[1].Exception.ShouldBeOfType<ArgumentOutOfRangeException>();
    }

    // Non-normative (INV-27): a zero delay still resubscribes asynchronously (after one millisecond), never on the stack of
    // the failure that caused it, so a pipeline failing on subscribe can't recurse.
    [Fact]
    public async Task Reactive_RetryDelayZero_ResubscribesAfterOneMillisecond()
    {
        await using var host = RetryingHost(static a => a < 3 ? TimeSpan.Zero : null);
        await host.StartAsync();
        host.Probe.Subscriptions.ShouldBe(1);

        host.Time.Advance(TimeSpan.FromMilliseconds(1));
        host.Probe.Subscriptions.ShouldBe(2);
        host.Time.Advance(TimeSpan.FromMilliseconds(1));
        host.Probe.Subscriptions.ShouldBe(3);
        host.Time.Advance(TimeSpan.FromDays(1));
        host.Probe.Subscriptions.ShouldBe(3);

        // Each failure is published: none of them runs under a failure action.
        await host.IdleAsync();
        host.Store.State.Get<IReadOnlyList<EffectFailed>>().Count.ShouldBe(3);
    }

    // §14, INV-27: a resubscribe pending when the store is disposed never resubscribes. Disposed during the delay, both
    // cancellations land in the one catch. The race between a retry that passed the re-check and the lifetime callback
    // that disposes the composite is driven through the seam: the composite is disposed, the token not yet cancelled, so
    // the retry finds its slot disposed at once and never calls Handle again (the Handle count stays 1). A retry already
    // past that check when the store is disposed is ReactiveInterleavingTests.Reactive_RetryRacingDispose_….
    [Fact]
    public async Task Reactive_ResubscribePendingAtDispose_NeverResubscribes()
    {
        var host = FlakyHost();
        await host.StartAsync();
        await DispatchIdleAsync(host, new Boom());

        await host.DisposeAsync();
        host.Time.Advance(TimeSpan.FromDays(1));

        host.Probe.Subscriptions.ShouldBe(1);

        var seam = new PipelineSeam();
        await using var raced = new Host(
            d => d.AddSlice<CounterSlice>().AddEffect<PipelineSeamEffect>().AddReactiveEffect<CompletingEffect>(),
            s => s.AddSingleton(seam));
        await raced.StartAsync();
        using var subscriptions = new CompositeDisposable();
        var probe = new Probe();
        new ReactivePipeline(new FlakyEffect(probe), seam.Bridge, seam.Context, subscriptions, new(NullLogger.Instance), TestContext.Current.CancellationToken).Subscribe();
        await DispatchIdleAsync(raced, new Boom());

        subscriptions.Dispose();
        raced.Time.Advance(TimeSpan.FromDays(1));

        probe.Subscriptions.ShouldBe(1);
    }

    // §14: StoreInitialized is pushed once, by the host, for the first subscriptions; a failure resubscribe never replays
    // it, so a reactive load never runs twice.
    [Fact]
    public async Task Reactive_OfActionTypeStoreInitialized_FiresOnceNotOnResubscribe()
    {
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddReactiveEffect<InitThenFlakyEffect>());
        await host.StartAsync();
        host.Probe.Seen.ShouldBe([new StoreInitialized()]);

        await DispatchIdleAsync(host, new Boom());
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.IdleAsync();

        host.Probe.Subscriptions.ShouldBe(2);
        host.Probe.Seen.ShouldBe([new StoreInitialized()]);
    }

    // §14, INV-27: the resubscribe lands between the Ping's commit and the bridge's State.Value write (a user AfterReduce
    // registered before ReactiveMiddleware advances the fake clock). The new pipeline's first value is the committed
    // snapshot, not the bridge's previous one, and the write that follows is not delivered twice. The witness, subscribed
    // throughout, while the failed pipeline left the subject during its OnNext and the new one joined, lost nothing.
    [Fact]
    public async Task Reactive_ResubscribeConcurrentWithDrain_FirstValueCurrent_NoLostSubscriber_Deterministic()
    {
        await using var host = new Host(d => d.AddSlice<CounterSlice>().Use<AdvanceOnPingMiddleware>().AddReactiveEffect<StateRunsFlakyEffect>().AddReactiveEffect<WitnessEffect>());
        await host.StartAsync();
        var initial = host.Store.State;
        await DispatchIdleAsync(host, new Boom());

        await DispatchIdleAsync(host, new Ping());

        host.Probe.Subscriptions.ShouldBe(2);
        var committed = host.Probe.AtAdvance.ShouldNotBeNull();
        committed.ShouldBeSameAs(host.Store.State);
        committed.Get<Counter>().Pings.ShouldBe(1);
        host.Probe.StateRuns[0].ShouldBe([initial]);
        host.Probe.StateRuns[1].ShouldHaveSingleItem().ShouldBeSameAs(committed);
        host.Probe.Seen.ShouldBe([initial, new StoreInitialized(), new Boom(), host.Probe.Seen[3], committed, new Ping()]);
        host.Probe.Seen[3].ShouldBeOfType<EffectFailed>().EffectType.ShouldBe(typeof(StateRunsFlakyEffect).ToString());
    }

    [Fact]
    public async Task Reactive_GroupByUntilRecipe_ReleasesGroups()
    {
        await using var host = new Host(d => d.AddSlice<ItemsSlice>().AddReactiveEffect<KeyedLoadEffect>());
        await host.StartAsync();

        // A keyed switch: the second load of item 1 cancels the first, item 2 runs beside it.
        await DispatchIdleAsync(host, new LoadItem(1));
        await DispatchIdleAsync(host, new LoadItem(2));
        host.Time.Advance(TimeSpan.FromMilliseconds(500));
        await DispatchIdleAsync(host, new LoadItem(1));
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.IdleAsync();
        host.Store.State.Get<IReadOnlyList<int>>().ShouldBe([2, 1]);
        (host.Probe.GroupsOpened, host.Probe.GroupsReleased).ShouldBe((2, 0));

        // Ten idle seconds release both groups; a later load opens a new one.
        host.Time.Advance(TimeSpan.FromSeconds(10));
        (host.Probe.GroupsOpened, host.Probe.GroupsReleased).ShouldBe((2, 2));
        await DispatchIdleAsync(host, new LoadItem(1));
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.IdleAsync();
        host.Store.State.Get<IReadOnlyList<int>>().ShouldBe([2, 1, 1]);
        (host.Probe.GroupsOpened, host.Probe.GroupsReleased).ShouldBe((3, 2));
    }

    // Non-normative (the recipe operator): the source's completion completes every open group, then the outer stream.
    [Fact]
    public void GroupByUntilRecipe_SourceCompletes_CompletesOpenGroups()
    {
        using var source = new Subject<int>();
        var completed = new List<string>();
        using var subscription = source.GroupByUntil(static i => i % 2, static _ => Observable.Never<Unit>())
            .Subscribe(g => g.Subscribe(static _ => { }, static _ => { }, _ => completed.Add("group")), static _ => { }, _ => completed.Add("outer"));

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);
        source.OnCompleted();

        completed.ShouldBe(["group", "group", "outer"]);
    }

    // Non-normative (§14): only a failure completion resubscribes; a pipeline that completes successfully has simply ended.
    [Fact]
    public async Task Reactive_SuccessfulCompletion_NeverResubscribesNorFails()
    {
        var logger = new FakeLogger<ReactiveHost>();
        await using var host = new Host(
            d => d.AddSlice<EffectFailureSlice>().AddReactiveEffect<CompletingEffect>(),
            s => s.AddSingleton<ILogger<ReactiveHost>>(logger));
        await host.StartAsync();
        host.Time.Advance(TimeSpan.FromDays(1));
        await host.IdleAsync();

        host.Probe.Subscriptions.ShouldBe(1);
        host.Store.State.Get<IReadOnlyList<EffectFailed>>().ShouldBeEmpty();
        Records(logger).ShouldBeEmpty();
    }
}
