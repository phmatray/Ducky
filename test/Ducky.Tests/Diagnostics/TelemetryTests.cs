using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Seen = Ducky.Tests.EffectFixtures.Seen;

namespace Ducky.Tests.Diagnostics;

// SPEC §6.3 (SafeTelemetry), §6.4 steps 1-3 and 11, §9 (tracing and metrics); INV-02, INV-03, INV-22. ActivitySource and
// MeterListener callbacks are process-wide and tests run in parallel, so every listener here samples or throws only for its
// own trace ids, and every metric is read through this test's own IMeterFactory (the Meter's Scope).
public sealed class TelemetryTests
{
    private const int TelemetryFailed = 1017;
    private const int Unexpected = 1012;

    // One inline drainer processes actions other flows enqueued: the action's span hangs under the producer's activity,
    // captured when the action was dispatched, never under the drainer's ambient activity or the span being processed. The
    // producer's flow is a captured ExecutionContext entered from inside a reducer (another thread's flow, without a
    // thread); the reducer's own child dispatch is a synchronous child of the span being processed. A producer flow without
    // any activity gets a root span, not one under the drainer's ambient activity (the BCL's fallback for a default parent).
    // The store is initialized first, so Dispatch drains inline on the caller's flow, not inside the init path.
    [Fact]
    public async Task Tracing_ActionFromOtherThread_ParentIsProducerActivity()
    {
        Activity producer = null!;
        ExecutionContext producerFlow = null!;
        ExecutionContext.Run(ExecutionContext.Capture()!, _ =>
        {
            producer = new Activity("producer").Start();
            producerFlow = ExecutionContext.Capture()!;
        }, null);
        ExecutionContext noActivityFlow = null!;
        ExecutionContext.Run(ExecutionContext.Capture()!, _ =>
        {
            Activity.Current = null;
            noActivityFlow = ExecutionContext.Capture()!;
        }, null);
        using var drainer = new Activity("drainer").Start();
        using var spans = new SpanRecorder(drainer.TraceId, producer.TraceId) { SampleRoots = true };
        var slice = new CountSlice();
        var store = new DuckyStore([slice], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        slice.OnProbe = () =>
        {
            store.Dispatch(new Boom());
            ExecutionContext.Run(producerFlow, _ => store.Dispatch(new Bump()), null);
            ExecutionContext.Run(noActivityFlow, _ => store.Dispatch(new Unparented()), null);
        };

        store.Dispatch(new Probe());

        Activity.Current.ShouldBeSameAs(drainer);
        var probe = spans.Single<Probe>();
        var child = spans.Single<Boom>();
        var produced = spans.Single<Bump>();
        probe.ParentSpanId.ShouldBe(drainer.SpanId);
        child.ParentSpanId.ShouldBe(probe.SpanId);
        produced.TraceId.ShouldBe(producer.TraceId);
        produced.ParentSpanId.ShouldBe(producer.SpanId);
        probe.OperationName.ShouldBe("ducky.dispatch");
        probe.Source.Name.ShouldBe("Ducky");
        probe.Kind.ShouldBe(ActivityKind.Internal);
        Tags(probe).ShouldBe(Expected(typeof(Probe), Origin.Local, 0, (long)Tag(probe, "ducky.correlation_id")!));
        Tags(child).ShouldBe(Expected(typeof(Boom), Origin.Local, 1, (long)Tag(probe, "ducky.correlation_id")!));
        ((long)Tag(produced, "ducky.correlation_id")!).ShouldBeGreaterThan((long)Tag(child, "ducky.correlation_id")!);
        Tag(produced, "ducky.depth").ShouldBe(0);
        var root = spans.Single<Unparented>();
        root.ParentSpanId.ShouldBe(default);
        root.TraceId.ShouldNotBe(drainer.TraceId);
        producer.Stop();
    }

    // Step 1 runs before step 2 installs the dropped action's scope, so the drainer's activity is still ambient there: the
    // ReducerFailed(DispatchLoopException) of a depth-dropped action takes that action's producer as its trace parent, as it
    // takes its correlation id, never the drainer's (§6.4 steps 1-2). The child Bump is produced under Probe's span and
    // dropped once the drainer's own activity is back.
    [Fact]
    public async Task Tracing_DepthDroppedAction_FailureParentIsProducerActivity()
    {
        using var drainer = new Activity("drainer").Start();
        using var spans = new SpanRecorder(drainer.TraceId);
        var slice = new CountSlice();
        var store = new DuckyStore([slice], NullLogger.Instance, maxDispatchDepth: 0);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        slice.OnProbe = () => store.Dispatch(new Bump());

        store.Dispatch(new Probe());

        var probe = spans.Single<Probe>();
        probe.ParentSpanId.ShouldBe(drainer.SpanId);
        spans.Single<ReducerFailed>().ParentSpanId.ShouldBe(probe.SpanId);
    }

    // Processing makes the action's span current, then restores the drainer's own Activity.Current, so no activity leaks
    // into the caller's flow after Dispatch returns, sampled or not: an activity a reducer started and never stopped is
    // dropped from the caller's flow even when no span was started (Stop of a span restores only what was current when the
    // span started). Initialized first: an async init path would hide a leak, since an async method's AsyncLocal changes
    // never flow back to its caller. An effect continuation that drains its own dispatch inline carries its trigger's span,
    // stopped by now, which the Activity.Current setter rejects (a swallowed first-chance InvalidOperationException): it
    // still gets that span back after Dispatch, with no rejected set, though the reducer leaked an activity.
    [Fact]
    public async Task Tracing_ActivityCurrentRestoredAfterDispatch()
    {
        using var caller = new Activity("caller").Start();
        using var spans = new SpanRecorder(caller.TraceId);
        List<Activity?> inside = [];
        List<Activity> leaked = [];
        var slice = new CountSlice();
        slice.OnProbe = () =>
        {
            inside.Add(Activity.Current);
            leaked.Add(new Activity("leaked").Start());
        };
        var store = new DuckyStore([slice], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        store.Dispatch(new Probe());

        Activity.Current.ShouldBeSameAs(caller);
        var span = inside.ShouldHaveSingleItem().ShouldNotBeNull();
        span.ShouldBeSameAs(spans.Single<Probe>());
        span.IsStopped.ShouldBeTrue();
        span.ParentSpanId.ShouldBe(caller.SpanId);
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
        Activity.Current.ShouldBeSameAs(caller);
        spans.Single<Bump>().ParentSpanId.ShouldBe(caller.SpanId);

        // No ambient activity, so nothing samples the action: no span, and still no leak.
        Activity.Current = null;
        store.Dispatch(new Probe());
        var unsampled = Activity.Current;
        Activity.Current = caller;
        unsampled.ShouldBeNull();
        inside[1].ShouldBeNull();

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Activity? before = null;
        Activity? after = null;
        var watched = 0;
        var rejected = 0;
        void OnThrow(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is InvalidOperationException && Environment.CurrentManagedThreadId == Volatile.Read(ref watched))
            {
                Interlocked.Increment(ref rejected);
            }
        }

        var continuation = new Handler<Bump>(async (_, context, _) =>
        {
            await gate.Task.ConfigureAwait(false);
            before = Activity.Current;
            Volatile.Write(ref watched, Environment.CurrentManagedThreadId);
            context.Dispatch(new Probe());
            Volatile.Write(ref watched, 0);
            after = Activity.Current;
        });
        var effectSlice = new CountSlice { OnProbe = () => leaked.Add(new Activity("leaked").Start()) };
        var effectStore = new DuckyStore([effectSlice], NullLogger.Instance, effects: () => [(continuation, false)]);
        await effectStore.InitializeAsync(TestContext.Current.CancellationToken);
        AppDomain.CurrentDomain.FirstChanceException += OnThrow;
        try
        {
            effectStore.Dispatch(new Bump());
            gate.SetResult();
            await effectStore.WhenIdleAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnThrow;
        }

        var trigger = before.ShouldNotBeNull();
        trigger.IsStopped.ShouldBeTrue();
        Tag(trigger, "ducky.action.type").ShouldBe(typeof(Bump).FullName);
        trigger.ParentSpanId.ShouldBe(caller.SpanId);
        after.ShouldBeSameAs(trigger);
        rejected.ShouldBe(0);
        leaked.Count.ShouldBe(3);
        leaked.ShouldAllBe(a => !a.IsStopped);
        leaked.ForEach(a => a.Stop());
    }

    // No ducky.dispatch span starts (no listener samples the caller's trace, the usual setup without AddSource("Ducky")): the
    // caller's activity stays current while its action is processed, so the reducer and an effect's synchronous prefix (whose
    // ExecutionContext carries it into later outbound spans) stay in the caller's trace, and it is still current afterwards.
    [Fact]
    public async Task Tracing_NoSpan_CallerActivityStaysCurrentDuringProcessing()
    {
        using var caller = new Activity("caller").Start();
        Activity? inReducer = null;
        Activity? inEffect = null;
        var slice = new CountSlice { OnProbe = () => inReducer = Activity.Current };
        var effect = new Handler<Probe>((_, _, _) =>
        {
            inEffect = Activity.Current;
            return Task.CompletedTask;
        });
        var store = new DuckyStore([slice], NullLogger.Instance, effects: () => [(effect, false)]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        store.Dispatch(new Probe());

        inReducer.ShouldBeSameAs(caller);
        inEffect.ShouldBeSameAs(caller);
        Activity.Current.ShouldBeSameAs(caller);
    }

    // Listener callbacks are user code that runs synchronously inside the steps. A throwing ActivityStarted and throwing
    // MeterListener callbacks: the depth drop still routes its ReducerFailed (INV-06), the Exhaust drop still lets the
    // action's other effect start, and the span whose ActivityStarted threw doesn't stay current. A throwing ActivityStopped: the action is reduced, the next one too, and Activity.Current
    // is still restored. Each throw is logged at Warning 1017, with the exception; nothing escapes to the catch-all (1012).
    [Fact]
    public async Task Telemetry_ListenerThrows_LaterStepsStillRun()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new Activity("root").Start();
        using var factory = new TestMeterFactory();
        using var listeners = new ThrowingListeners(factory, root.TraceId)
        {
            OnStart = () => new InvalidOperationException("started"),
            OnMeasure = _ => new InvalidOperationException("measured"),
        };
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        List<int> started = [];
        List<Activity?> current = [];
        var exhaust = new ExhaustHandler(_ => gate.Task);
        var other = new Handler<Load>((load, _, _) =>
        {
            started.Add(load.Id);
            current.Add(Activity.Current);
            return Task.CompletedTask;
        });
        var store = new DuckyStore(
            [new TelemetrySlice()], logger, maxDispatchDepth: 0, effects: () => [(exhaust, false), (other, false), (Looping(), false)], meterFactory: factory);

        (await store.DispatchAsync(new Loop())).ShouldBe(DispatchResult.Reduced);
        store.Dispatch(new Load(1));
        (await store.DispatchAsync(new Load(2))).ShouldBe(DispatchResult.Reduced);

        started.ShouldBe([1, 2]);

        // The throwing ActivityStarted ran after Start made the span current: the span is dropped and Activity.Current goes back
        // to the root activity, so nothing created while processing (an effect's prefix here) hangs under a span never stopped.
        current.ShouldAllBe(a => a == root);
        var seen = store.State.Get<Seen>().Actions;
        seen.OfType<ReducerFailed>().ShouldHaveSingleItem().Exception.ShouldBeOfType<DispatchLoopException>();
        listeners.Measured.ShouldContain("ducky.dispatch.dropped");
        listeners.Measured.ShouldContain("ducky.effect.dropped");
        listeners.Measured.ShouldContain("ducky.dispatch.duration");
        Activity.Current.ShouldBeSameAs(root);

        listeners.OnStart = null;
        listeners.OnStop = () => new InvalidOperationException("stopped");
        var collector = logger.Collector;
        collector.Clear();
        (await store.DispatchAsync(new Load(3))).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new Load(4))).ShouldBe(DispatchResult.Reduced);
        started.ShouldBe([1, 2, 3, 4]);
        listeners.Stopped.ShouldBeGreaterThanOrEqualTo(2);
        Activity.Current.ShouldBeSameAs(root);
        var stopFailures = collector.GetSnapshot().Where(r => r.Id.Id == TelemetryFailed).ToList();
        stopFailures.ShouldNotBeEmpty();
        stopFailures.ShouldAllBe(r => r.Exception!.Message == "stopped" || r.Exception.Message == "measured");

        gate.SetResult();
        await store.WhenIdleAsync(ct);
        var all = store.State.Get<Seen>().Actions;
        all.OfType<Load>().Select(l => l.Id).ShouldBe([1, 2, 3, 4]);

        // DropEffect runs inside the Exhaust run's try: an unwrapped Add would turn into an EffectFailed, not stop the others.
        all.OfType<EffectFailed>().ShouldBeEmpty();
        collector.GetSnapshot().ShouldNotContain(r => r.Id.Id == Unexpected);
        var failure = collector.GetSnapshot().First(r => r.Id.Id == TelemetryFailed);
        failure.Level.ShouldBe(LogLevel.Warning);
        failure.Message.ShouldStartWith("A telemetry listener threw in ");
    }

    // Each wrapped call logs which call failed: StartActivity and Add on the depth drop, Record and Stop for the action.
    [Fact]
    public async Task Telemetry_ListenerThrows_EachCallLoggedWithItsName()
    {
        using var root = new Activity("root").Start();
        using var factory = new TestMeterFactory();
        using var listeners = new ThrowingListeners(factory, root.TraceId)
        {
            OnStart = () => new InvalidOperationException("started"),
            OnMeasure = _ => new InvalidOperationException("measured"),
        };
        var logger = new FakeLogger();
        var store = new DuckyStore([new TelemetrySlice()], logger, maxDispatchDepth: 0, effects: () => [(Looping(), false)], meterFactory: factory);

        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);
        listeners.OnStart = null;
        listeners.OnStop = () => new InvalidOperationException("stopped");
        (await store.DispatchAsync(new Load(2))).ShouldBe(DispatchResult.Reduced);

        var messages = logger.Collector.GetSnapshot().Where(r => r.Id.Id == TelemetryFailed).Select(r => r.Message).Distinct().ToList();
        messages.ShouldBe(
            [
                "A telemetry listener threw in StartActivity; the step goes on",
                "A telemetry listener threw in Record; the step goes on",
                "A telemetry listener threw in Stop; the step goes on",
            ],
            ignoreOrder: true);

        logger.Collector.Clear();
        (await store.DispatchAsync(new Loop())).ShouldBe(DispatchResult.Reduced);
        logger.Collector.GetSnapshot().Where(r => r.Id.Id == TelemetryFailed).Select(r => r.Message)
            .ShouldContain("A telemetry listener threw in Add; the step goes on");
    }

    // OutOfMemoryException is fatal (§10): like SafeLogger, SafeTelemetry lets it through, so the drain's catch-all (Error
    // 1012) sees it, for each of the four wrapped calls. Process restores its scopes before the telemetry calls of its
    // finally, so a fatal Stop or Record leaves no causal scope or activity behind in the drainer's flow.
    [Fact]
    public async Task Telemetry_ListenerThrowsFatal_ReachesCatchAll()
    {
        using var root = new Activity("root").Start();
        using var factory = new TestMeterFactory();
        using var listeners = new ThrowingListeners(factory, root.TraceId);
        var logger = new FakeLogger();
        var store = new DuckyStore([new TelemetrySlice()], logger, meterFactory: factory);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        listeners.OnStart = () => ThrowingLogger.Fatal();

        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Failed);
        listeners.OnStart = null;
        listeners.OnStop = () => ThrowingLogger.Fatal();
        (await store.DispatchAsync(new Load(2))).ShouldBe(DispatchResult.Reduced);
        store.Dispatcher.Causal.ShouldBeNull();
        Activity.Current.ShouldBeSameAs(root);
        listeners.OnStop = null;
        listeners.OnMeasure = name => name == "ducky.dispatch.duration" ? ThrowingLogger.Fatal() : null;
        (await store.DispatchAsync(new Load(3))).ShouldBe(DispatchResult.Reduced);
        store.Dispatcher.Causal.ShouldBeNull();
        Activity.Current.ShouldBeSameAs(root);
        listeners.OnMeasure = name => name == "ducky.dispatch.dropped" ? ThrowingLogger.Fatal() : null;
        var loop = new DuckyStore([new TelemetrySlice()], logger, maxDispatchDepth: 0, effects: () => [(Looping(), false)], meterFactory: factory);
        await loop.InitializeAsync(TestContext.Current.CancellationToken);
        (await loop.DispatchAsync(new Loop())).ShouldBe(DispatchResult.Reduced);
        listeners.OnMeasure = null;

        var fatal = logger.Collector.GetSnapshot().Where(r => r.Id.Id == Unexpected).ToList();
        fatal.Count.ShouldBe(4);
        fatal.ShouldAllBe(r => r.Exception is OutOfMemoryException);
        logger.Collector.GetSnapshot().ShouldNotContain(r => r.Id.Id == TelemetryFailed);
    }

    // Through AddDucky with an IMeterFactory: ducky.dispatch.dropped with its reason (the depth drop and the call-time drop
    // of a superseded Switch run), ducky.effect.failures, ducky.effect.dropped (Exhaust) and ducky.dispatch.duration in ms
    // (TimeProvider's clock, advanced inside a reducer). A dispatch after disposal began completes Disposed, uncounted.
    [Fact]
    public async Task Metrics_DroppedAndEffectCounters()
    {
        using var factory = new TestMeterFactory();
        var time = new FakeTimeProvider();
        var gate = new TaskCompletionSource();
        var hold = new TaskCompletionSource();
        EffectContext? held = null;
        var services = new ServiceCollection()
            .AddSingleton<IMeterFactory>(factory)
            .AddSingleton<TimeProvider>(time)
            .AddLogging(logging => logging.AddFakeLogging());
        services.AddDucky(d =>
        {
            d.MaxDispatchDepth = 0;
            d.AddSlice<TelemetrySlice>();
            d.AddEffect(new ExhaustHandler(_ => hold.Task));
            d.AddEffect(Looping());
            d.AddEffect(new Handler<Boom>((_, _, _) => throw new InvalidOperationException("effect")));
            d.AddEffect(new Handler<Advance>((_, _, _) =>
            {
                time.Advance(TimeSpan.FromMilliseconds(5));
                return Task.CompletedTask;
            }));
            d.AddEffect(new SwitchHandler<Bump>(async (_, context, _) =>
            {
                held ??= context;
                await gate.Task.ConfigureAwait(false);
            }));
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        using var dropped = new MetricCollector<long>(factory, "Ducky", "ducky.dispatch.dropped");
        using var failures = new MetricCollector<long>(factory, "Ducky", "ducky.effect.failures");
        using var effectDropped = new MetricCollector<long>(factory, "Ducky", "ducky.effect.dropped");
        using var duration = new MetricCollector<double>(factory, "Ducky", "ducky.dispatch.duration");

        // The runs wait on gate and hold, and the scope's dispose waits for them under a clock that never moves: a failed
        // assertion must still release them.
        try
        {
            (await store.DispatchAsync(new Loop())).ShouldBe(DispatchResult.Reduced);
            (await store.DispatchAsync(new Boom())).ShouldBe(DispatchResult.Reduced);
            store.Dispatch(new Load(1));
            store.Dispatch(new Load(2));
            store.Dispatch(new Bump());
            store.Dispatch(new Bump());
            (await held.ShouldNotBeNull().DispatchAsync(new Probe())).ShouldBe(DispatchResult.Dropped);
            (await store.DispatchAsync(new Advance())).ShouldBe(DispatchResult.Reduced);

            dropped.GetMeasurementSnapshot().Select(m => (m.Value, m.Tags["ducky.drop.reason"])).ShouldBe([(1L, (object?)"depth"), (1L, "run")]);
            failures.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
            effectDropped.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
            duration.Instrument!.Unit.ShouldBe("ms");
            duration.GetMeasurementSnapshot().Select(m => m.Value).ShouldContain(5d);
            duration.GetMeasurementSnapshot().Select(m => m.Value).ShouldAllBe(v => v == 0d || v == 5d);

            var disposing = store.DisposeAsync();
            (await held.DispatchAsync(new Probe())).ShouldBe(DispatchResult.Disposed);
            gate.SetResult();
            hold.SetResult();
            await disposing;
            dropped.GetMeasurementSnapshot().Count.ShouldBe(2);
        }
        finally
        {
            gate.TrySetResult();
            hold.TrySetResult();
        }
    }

    // Without an IMeterFactory the store creates no Meter at all (no static Meter, §6.12): a MeterListener started before
    // the store sees no instrument of an unscoped "Ducky" meter, even across drops and an effect failure.
    [Fact]
    public async Task Metrics_NoMeterFactory_NoMeterCreated()
    {
        List<string> published = [];
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter is { Name: "Ducky", Scope: null })
                {
                    lock (published)
                    {
                        published.Add(instrument.Name);
                    }
                }
            },
        };
        listener.Start();
        var services = new ServiceCollection().AddLogging();
        services.AddDucky(d =>
        {
            d.MaxDispatchDepth = 0;
            d.AddSlice<TelemetrySlice>();
            d.AddEffect(Looping());
            d.AddEffect(new Handler<Boom>((_, _, _) => throw new InvalidOperationException("effect")));
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();

        (await store.DispatchAsync(new Loop())).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new Boom())).ShouldBe(DispatchResult.Reduced);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        lock (published)
        {
            published.ShouldBeEmpty();
        }
    }

    // Dispatches another Loop from its synchronous prefix: a synchronous child, so with MaxDispatchDepth 0 it is depth-dropped.
    private static Handler<Loop> Looping() => new((_, context, _) =>
    {
        context.Dispatch(new Loop());
        return Task.CompletedTask;
    });

    private static object? Tag(Activity activity, string key) => activity.GetTagItem(key);

    private static Dictionary<string, object?> Tags(Activity activity) => activity.TagObjects.ToDictionary(t => t.Key, t => t.Value);

    private static Dictionary<string, object?> Expected(Type type, Origin origin, int depth, long correlationId) => new()
    {
        ["ducky.action.type"] = type.FullName,
        ["ducky.origin"] = origin.ToString(),
        ["ducky.depth"] = depth,
        ["ducky.correlation_id"] = correlationId,
    };

    internal sealed record Loop;

    internal sealed record Advance;

    internal sealed record Unparented;

    // Records every action it sees.
    internal sealed class TelemetrySlice : Slice<Seen>
    {
        public TelemetrySlice()
        {
            On<Load>(Add);
            On<Loop>(Add);
            On<Boom>(Add);
            On<Bump>(Add);
            On<Probe>(Add);
            On<ReducerFailed>(Add);
            On<EffectFailed>(Add);
            On<Advance>(Add);
        }

        protected override Seen Initial => new([]);

        private static Seen Add<TAction>(Seen state, TAction action)
            where TAction : notnull => new([.. state.Actions, action]);
    }

    private sealed class ExhaustHandler(Func<Load, Task> handle) : Effect<Load>
    {
        public override Concurrency Policy => Concurrency.Exhaust;

        public override Task Handle(Load action, EffectContext context, CancellationToken cancellationToken) => handle(action);
    }

    // A real IMeterFactory has no package here (Microsoft.Extensions.Diagnostics is not referenced); this one scopes every
    // Meter it creates to itself, as the framework's does, so MetricCollector can filter on it.
    internal sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(new MeterOptions(options.Name) { Version = options.Version, Tags = options.Tags, Scope = this });
            lock (_meters)
            {
                _meters.Add(meter);
            }

            return meter;
        }

        public void Dispose()
        {
            lock (_meters)
            {
                _meters.ForEach(m => m.Dispose());
            }
        }
    }

    // Samples and records the "Ducky" spans of the given traces only, plus root spans started on the flow that set
    // SampleRoots (an AsyncLocal, so other tests' root spans stay unsampled).
    private sealed class SpanRecorder : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly ConcurrentQueue<Activity> _stopped = new();
        private readonly AsyncLocal<bool> _roots = new();

        public SpanRecorder(params ActivityTraceId[] traces)
        {
            _listener = new()
            {
                ShouldListenTo = source => source.Name == "Ducky",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    traces.Contains(options.Parent.TraceId) || (options.Parent == default && _roots.Value)
                        ? ActivitySamplingResult.AllDataAndRecorded
                        : ActivitySamplingResult.None,
                ActivityStopped = activity =>
                {
                    if (traces.Contains(activity.TraceId) || (activity.ParentSpanId == default && _roots.Value))
                    {
                        _stopped.Enqueue(activity);
                    }
                },
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public bool SampleRoots
        {
            set => _roots.Value = value;
        }

        // The tag is the DT-02 type name (§9): nested types join with '.', not FullName's '+'.
        public Activity Single<TAction>() =>
            _stopped.Single(a => (string?)a.GetTagItem("ducky.action.type") == typeof(TAction).FullName!.Replace('+', '.'));

        public void Dispose() => _listener.Dispose();
    }

    // An ActivityListener and a MeterListener whose callbacks throw what OnStart, OnStop and OnMeasure return (nothing when
    // null), for this trace and this factory's meters only.
    private sealed class ThrowingListeners : IDisposable
    {
        private readonly ActivityListener _activities;
        private readonly MeterListener _meters;
        private readonly ConcurrentQueue<string> _measured = new();
        private int _stopped;

        public ThrowingListeners(IMeterFactory factory, ActivityTraceId trace)
        {
            _activities = new()
            {
                ShouldListenTo = source => source.Name == "Ducky",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    options.Parent.TraceId == trace ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None,
                ActivityStarted = activity =>
                {
                    if (activity.TraceId == trace && OnStart?.Invoke() is { } ex)
                    {
                        throw ex;
                    }
                },
                ActivityStopped = activity =>
                {
                    if (activity.TraceId == trace)
                    {
                        Interlocked.Increment(ref _stopped);
                        if (OnStop?.Invoke() is { } ex)
                        {
                            throw ex;
                        }
                    }
                },
            };
            ActivitySource.AddActivityListener(_activities);
            _meters = new()
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Scope == factory)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _meters.SetMeasurementEventCallback<long>((instrument, _, _, _) => Measure(instrument.Name));
            _meters.SetMeasurementEventCallback<double>((instrument, _, _, _) => Measure(instrument.Name));
            _meters.Start();
        }

        public Func<Exception?>? OnStart { get; set; }

        public Func<Exception?>? OnStop { get; set; }

        public Func<string, Exception?>? OnMeasure { get; set; }

        public IReadOnlyCollection<string> Measured => _measured;

        public int Stopped => Volatile.Read(ref _stopped);

        public void Dispose()
        {
            _activities.Dispose();
            _meters.Dispose();
        }

        private void Measure(string name)
        {
            _measured.Enqueue(name);
            if (OnMeasure?.Invoke(name) is { } ex)
            {
                throw ex;
            }
        }
    }

    // An inline drainer whose ambient activity is stopped (an effect continuation carrying its trigger's span): a throwing
    // ActivityStarted can't put that stopped activity back (the setter rejects it), so processing runs with none rather than
    // under the span never stopped, and the stopped ambient is current again once Dispatch returns.
    [Fact]
    public async Task Telemetry_ListenerThrowsUnderStoppedAmbient_AmbientComesBack()
    {
        using var root = new Activity("root").Start();
        using var factory = new TestMeterFactory();
        using var listeners = new ThrowingListeners(factory, root.TraceId) { OnStart = () => new InvalidOperationException("started") };
        var logger = new FakeLogger();
        Activity? inReducer = null;
        var slice = new CountSlice { OnProbe = () => inReducer = Activity.Current };
        var store = new DuckyStore([slice], logger);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        logger.Collector.Clear(); // StoreInitialized's span threw too
        var (trigger, stoppedFlow) = StoppedAmbient();
        Task<DispatchResult> result = null!;
        Activity? after = null;

        ExecutionContext.Run(stoppedFlow, _ =>
        {
            result = store.DispatchAsync(new Probe());
            after = Activity.Current;
        }, null);

        (await result).ShouldBe(DispatchResult.Reduced);
        after.ShouldBeSameAs(trigger);
        inReducer.ShouldBeNull();
        var failure = logger.Collector.GetSnapshot().Where(r => r.Id.Id == TelemetryFailed).ShouldHaveSingleItem();
        failure.Level.ShouldBe(LogLevel.Warning);
        failure.Exception!.Message.ShouldBe("started");
    }

    // ExecutionContext.Capture is null while the flow is suppressed: an inline drainer under a stopped ambient activity still
    // gets it back after Dispatch, though a reducer started an activity it never stopped (§6.4 step 2).
    [Fact]
    public async Task Tracing_SuppressedFlowUnderStoppedAmbient_AmbientComesBack()
    {
        List<Activity> leaked = [];
        var slice = new CountSlice { OnProbe = () => leaked.Add(new Activity("leaked").Start()) };
        var store = new DuckyStore([slice], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var (trigger, stoppedFlow) = StoppedAmbient();
        Activity? after = null;

        ExecutionContext.Run(stoppedFlow, _ =>
        {
            using (ExecutionContext.SuppressFlow())
            {
                store.Dispatch(new Probe());
                after = Activity.Current;
            }
        }, null);

        leaked.ShouldHaveSingleItem().IsStopped.ShouldBeFalse();
        after.ShouldBeSameAs(trigger);
        leaked.ForEach(a => a.Stop());
    }

    // A child of the current activity, stopped, and a flow on which it is still current.
    private static (Activity Trigger, ExecutionContext Flow) StoppedAmbient()
    {
        Activity trigger = null!;
        ExecutionContext flow = null!;
        ExecutionContext.Run(ExecutionContext.Capture()!, _ =>
        {
            trigger = new Activity("trigger").Start();
            flow = ExecutionContext.Capture()!;
            trigger.Stop();
        }, null);
        return (trigger, flow);
    }

    // With no span (nobody samples), the reducers and the effects' synchronous prefixes run under the producer's activity,
    // whichever flow drains (§6.3): a live one from another flow becomes ambient, never the drainer's; a stopped one from
    // another flow can't be set (the setter rejects it), so none is. An inline hierarchical-id activity (whose Context is
    // default) stays ambient too.
    [Fact]
    public async Task Tracing_NoSpan_ProducerActivityAmbientWhicheverFlowDrains()
    {
        Activity live = null!;
        ExecutionContext liveFlow = null!;
        ExecutionContext.Run(ExecutionContext.Capture()!, _ =>
        {
            live = new Activity("live").Start();
            liveFlow = ExecutionContext.Capture()!;
        }, null);
        var (_, stoppedFlow) = StoppedAmbient();
        using var drainer = new Activity("drainer").Start();
        Dictionary<Type, Activity?> seen = [];
        Handler<TAction> Record<TAction>()
            where TAction : notnull => new((_, _, _) =>
        {
            seen[typeof(TAction)] = Activity.Current;
            return Task.CompletedTask;
        });
        var slice = new CountSlice();
        var store = new DuckyStore([slice], NullLogger.Instance, effects: () => [(Record<Bump>(), false), (Record<Boom>(), false)]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        slice.OnProbe = () =>
        {
            ExecutionContext.Run(liveFlow, _ => store.Dispatch(new Bump()), null);
            ExecutionContext.Run(stoppedFlow, _ => store.Dispatch(new Boom()), null);
        };

        store.Dispatch(new Probe());

        seen[typeof(Bump)].ShouldBeSameAs(live);
        seen[typeof(Boom)].ShouldBeNull();
        Activity.Current.ShouldBeSameAs(drainer);

        using (var hierarchical = new Activity("hierarchical").SetIdFormat(ActivityIdFormat.Hierarchical).Start())
        {
            store.Dispatch(new Bump());
            seen[typeof(Bump)].ShouldBeSameAs(hierarchical);
            Activity.Current.ShouldBeSameAs(hierarchical);
        }

        Activity.Current.ShouldBeSameAs(drainer);
        live.Stop();
    }
}
