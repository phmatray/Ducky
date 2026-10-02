using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.InitFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests;

// SPEC §5.6 (Middleware, ActionContext), §5.1 (Use<T>), §6.4 steps 4, 5 and 9, §6.11 phase 5a; INV-12, INV-29.
public sealed class MiddlewareTests
{
    [Fact]
    public async Task Veto_NeverAppliesToSystemOrRestoreOrigins()
    {
        var journal = new List<string>();
        var veto = new Recorder("veto", journal) { Allow = _ => false };
        var trail = new TrailSlice { OnInit = () => throw new InvalidOperationException("init") };
        var failures = new FailureSlice();
        var store = new DuckyStore([trail, new CountSlice(), failures], NullLogger.Instance, middleware: () => [veto]);

        // System: StoreInitialized, and the ReducerFailed its throwing reducer raised, are both reduced.
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        failures.Failures.ShouldHaveSingleItem().ActionType.ShouldBe(typeof(StoreInitialized).ToString());

        // Restore origins.
        store.Restore(new Dictionary<string, object> { ["count"] = new Count(1) }, Origin.Hydration);
        store.Restore(new Dictionary<string, object> { ["count"] = new Count(2) }, Origin.CrossTab);
        store.Restore(new Dictionary<string, object> { ["count"] = new Count(3) }, Origin.DevTools);
        store.State.Get<Count>().Value.ShouldBe(3);

        // User origins are vetoed: Local, and Effect (EffectContext comes with M2-01, so through the internal path).
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Vetoed);
        var fromEffect = new TaskCompletionSource<DispatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Dispatcher.Enqueue(store.Dispatcher.NewPending(new Bump(), Origin.Effect, fromEffect));
        (await fromEffect.Task).ShouldBe(DispatchResult.Vetoed);
        store.State.Get<Count>().Value.ShouldBe(3);

        journal.Where(e => e.StartsWith("veto.MayDispatch", StringComparison.Ordinal))
            .ShouldBe(["veto.MayDispatch Bump", "veto.MayDispatch Bump"]);
        veto.Contexts.Where(c => c.Action is Bump).Select(c => c.Origin).ShouldBe([Origin.Local, Origin.Effect]);
    }

    // Non-normative: the first false vetoes (Debug 1020) and ends Process: no later MayDispatch, no BeforeReduce, no
    // reduce, no AfterReduce (§6.4 step 4).
    [Fact]
    public async Task MayDispatch_FirstFalse_VetoesAndEndsProcess()
    {
        var logger = new FakeLogger();
        var journal = new List<string>();
        var first = new Recorder("first", journal);
        var veto = new Recorder("veto", journal) { Allow = c => c.Action is not Bump };
        var last = new Recorder("last", journal);
        var slice = new CountSlice();
        var store = new DuckyStore([slice], logger, middleware: () => [first, veto, last]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        journal.Clear();

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Vetoed);

        journal.ShouldBe(["first.MayDispatch Bump", "veto.MayDispatch Bump"]);
        slice.Reduced.ShouldBeEmpty();
        store.State.Version.ShouldBe(0);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1020);
        record.Level.ShouldBe(LogLevel.Debug);
        record.Message.ShouldBe($"{typeof(Bump)} vetoed by {typeof(Recorder)}");
    }

    // Non-normative (INV-12): a throwing MayDispatch fails the action and becomes one ReducerFailed with no slice key; the
    // action was never admitted, so nothing else runs for it.
    [Fact]
    public async Task MayDispatch_Throws_FailedAndOneReducerFailed()
    {
        var logger = new FakeLogger();
        var journal = new List<string>();
        var thrown = new InvalidOperationException("veto");
        var throwing = new Recorder("throwing", journal) { Allow = c => c.Action is Bump ? throw thrown : true };
        var last = new Recorder("last", journal);
        var failures = new FailureSlice();
        var store = new DuckyStore([new CountSlice(), failures], logger, middleware: () => [throwing, last]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        journal.Clear();

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Failed);

        var failed = failures.Failures.ShouldHaveSingleItem();
        failed.ActionType.ShouldBe(typeof(Bump).ToString());
        failed.SliceKey.ShouldBeNull();
        failed.Exception.ShouldBeSameAs(thrown);
        store.State.Get<Count>().Value.ShouldBe(0);
        journal.Where(e => e.EndsWith(" Bump", StringComparison.Ordinal)).ShouldBe(["throwing.MayDispatch Bump"]);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1021);
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeSameAs(thrown);
        record.Message.ShouldBe($"{typeof(Recorder)}.MayDispatch threw while handling {typeof(Bump)}");
    }

    // Non-normative (INV-12): BeforeReduce runs on each middleware in registration order; the first throw fails the
    // action, commits nothing and becomes one ReducerFailed with no slice key.
    [Fact]
    public async Task BeforeReduce_Throws_FailedNothingCommittedOneReducerFailed()
    {
        var logger = new FakeLogger();
        var journal = new List<string>();
        var thrown = new InvalidOperationException("before");
        var first = new Recorder("first", journal);
        var throwing = new Recorder("throwing", journal) { OnBefore = c => ThrowIf(c.Action is Bump, thrown) };
        var last = new Recorder("last", journal);
        var slice = new CountSlice();
        var failures = new FailureSlice();
        var store = new DuckyStore([slice, failures], logger, middleware: () => [first, throwing, last]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        journal.Clear();

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Failed);

        journal.Where(e => e.Contains(".BeforeReduce Bump", StringComparison.Ordinal))
            .ShouldBe(["first.BeforeReduce Bump", "throwing.BeforeReduce Bump"]);
        slice.Reduced.ShouldBeEmpty();
        store.State.Get<Count>().Value.ShouldBe(0);
        var failed = failures.Failures.ShouldHaveSingleItem();
        failed.ActionType.ShouldBe(typeof(Bump).ToString());
        failed.SliceKey.ShouldBeNull();
        failed.Exception.ShouldBeSameAs(thrown);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1021);
        record.Exception.ShouldBeSameAs(thrown);
        record.Message.ShouldBe($"{typeof(Recorder)}.BeforeReduce threw while handling {typeof(Bump)}");
    }

    // Non-normative (INV-12): a hook that throws while handling the failure action is logged, never dispatched.
    [Fact]
    public async Task BeforeReduce_ThrowsOnFailureAction_LogsOnly()
    {
        var logger = new FakeLogger();
        var armed = false;
        var throwing = new Recorder("throwing", []) { OnBefore = _ => ThrowIf(armed, new InvalidOperationException("before")) };
        var failures = new FailureSlice();
        var store = new DuckyStore([new CountSlice(), failures], logger, middleware: () => [throwing]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        armed = true;

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Failed);

        failures.Failures.ShouldBeEmpty();
        logger.Collector.GetSnapshot().Select(r => r.Id.Id).ShouldBe([1021, 1021, 1001]);
        armed = false;
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
    }

    [Fact]
    public async Task Middleware_AfterReduceThrows_OthersStillRun()
    {
        var logger = new FakeLogger();
        var journal = new List<string>();
        var thrown = new InvalidOperationException("after");
        var throwing = new Recorder("throwing", journal) { OnAfter = _ => throw thrown };
        var last = new Recorder("last", journal);
        var store = new DuckyStore([new CountSlice()], logger, middleware: () => [throwing, last]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        logger.Collector.Clear();

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);

        journal.Where(e => e.Contains(".AfterReduce", StringComparison.Ordinal)).ShouldBe(
            ["throwing.AfterReduce StoreInitialized", "last.AfterReduce StoreInitialized", "throwing.AfterReduce Bump", "last.AfterReduce Bump"]);
        store.State.Get<Count>().Value.ShouldBe(1);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1021);
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeSameAs(thrown);
        record.Message.ShouldBe($"{typeof(Recorder)}.AfterReduce threw while handling {typeof(Bump)}");
    }

    // Non-normative: the hooks before the reduce see State == PreviousState and no changed key; AfterReduce sees the
    // committed snapshot and exactly the keys whose slot changed reference (§5.6). The default hooks admit and observe.
    [Fact]
    public async Task ActionContext_CarriesActionScopeAndStates()
    {
        var recorder = new Recorder("r", []);
        var store = new DuckyStore([new CountSlice(), new OtherSlice()], NullLogger.Instance, middleware: () => [new Bare(), recorder]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        recorder.Contexts.Clear();
        var before = store.State;
        var bump = new Bump();

        (await store.DispatchAsync(bump)).ShouldBe(DispatchResult.Reduced);

        recorder.Contexts.Count.ShouldBe(3);
        foreach (var context in recorder.Contexts)
        {
            context.Action.ShouldBeSameAs(bump);
            context.ActionType.ShouldBe(typeof(Bump).ToString());
            context.Origin.ShouldBe(Origin.Local);
            context.Depth.ShouldBe(0);
            context.Id.ShouldBe(2); // StoreInitialized is Id 1
            context.CorrelationId.ShouldBe(context.Id);
            context.PreviousState.ShouldBeSameAs(before);
        }

        recorder.Contexts[0].State.ShouldBeSameAs(before);
        recorder.Contexts[0].ChangedKeys.ShouldBeEmpty();
        recorder.Contexts[1].State.ShouldBeSameAs(before);
        recorder.Contexts[1].ChangedKeys.ShouldBeEmpty();
        recorder.Contexts[2].State.ShouldBeSameAs(store.State);
        recorder.Contexts[2].State.Get<Count>().Value.ShouldBe(1);
        recorder.Contexts[2].ChangedKeys.ShouldBe(["count"]);

        // One context is shared by every AfterReduce: a middleware can't rewrite the keys the next one sees.
        Should.Throw<NotSupportedException>(() => ((IList<string>)recorder.Contexts[2].ChangedKeys)[0] = "other");
    }

    // Non-normative: a handled action that returns the same state changes no key, and a dispatch from AfterReduce is a
    // synchronous child on the same chain (§6.5).
    [Fact]
    public async Task ActionContext_UnchangedAndChildActions()
    {
        var recorder = new Recorder("r", []);
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance, middleware: () => [recorder]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        recorder.OnAfter = c =>
        {
            if (c.Action is Probe)
            {
                store.Dispatch(new Bump());
            }
        };
        recorder.Contexts.Clear();

        (await store.DispatchAsync(new Probe())).ShouldBe(DispatchResult.Reduced);

        var probe = recorder.Contexts[2];
        probe.Action.ShouldBeOfType<Probe>();
        probe.ChangedKeys.ShouldBeEmpty();
        probe.State.ShouldBeSameAs(probe.PreviousState);
        var child = recorder.Contexts[5];
        child.Action.ShouldBeOfType<Bump>();
        child.Depth.ShouldBe(1);
        child.CorrelationId.ShouldBe(probe.Id);
        child.Id.ShouldBeGreaterThan(probe.Id);
        child.PreviousState.ShouldBeSameAs(probe.State);
        child.ChangedKeys.ShouldBe(["count"]);
    }

    public static TheoryData<ServiceLifetime> Lifetimes => new() { ServiceLifetime.Singleton, ServiceLifetime.Scoped };

    // Non-normative: Use<T> is idempotent and the first registration fixes the position; middleware is created from the
    // store's services, so a scoped dependency resolves in both lifetimes: the store scope (Singleton) or the DI scope
    // (Scoped) (§5.1, §6.10).
    [Theory]
    [MemberData(nameof(Lifetimes))]
    public async Task Use_SameTypeTwice_OneInstanceAtFirstPosition(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection()
            .AddSingleton<Journal>()
            .AddScoped<ScopedDependency>()
            .AddDucky(d =>
            {
                d.Lifetime = lifetime;
                d.AddSlice<CountSlice>();
                d.Use<FirstMiddleware>().Use<SecondMiddleware>().Use<FirstMiddleware>();
            });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        var journal = provider.GetRequiredService<Journal>();

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);

        journal.Instances.Count.ShouldBe(1);
        journal.Entries.ShouldBe(["First StoreInitialized", "Second StoreInitialized", "First Bump", "Second Bump"]);
    }

    // DisposeTimeout is the bound the store actually waits with: Task.WaitAsync rejects a negative bound (Infinite aside)
    // and one over uint.MaxValue - 1 ms, so the store clamps it, and a flush bounded by DisposeTimeout never throws.
    public static TheoryData<TimeSpan, TimeSpan> DisposeTimeouts => new()
    {
        { TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(7) },
        { Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan },
        { TimeSpan.MaxValue, TimeSpan.FromTicks((uint.MaxValue - 1L) * TimeSpan.TicksPerMillisecond) },
        { TimeSpan.FromSeconds(-1), TimeSpan.Zero },
    };

    // Non-normative: Store and DisposeTimeout throw InvalidOperationException until the store attaches them, which it
    // does when it creates the middleware (before any hook or InitializeAsync can run).
    [Theory]
    [MemberData(nameof(DisposeTimeouts))]
    public async Task Middleware_StoreAndDisposeTimeout_AttachedByStore(TimeSpan configured, TimeSpan expected)
    {
        var detached = new Recorder("r", []);
        Should.Throw<InvalidOperationException>(() => detached.AttachedStore);
        Should.Throw<InvalidOperationException>(() => detached.AttachedDisposeTimeout);

        var services = new ServiceCollection()
            .AddSingleton<Journal>()
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Scoped;
                d.DisposeTimeout = configured;
                d.AddSlice<CountSlice>();
                d.Use<FirstMiddleware>();
            });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // Read from inside the first hook, so both are attached before any hook runs.
        var middleware = provider.GetRequiredService<Journal>().Instances.ShouldHaveSingleItem();
        var (attachedStore, attachedTimeout) = middleware.AtFirstHook.ShouldNotBeNull();
        attachedStore.ShouldBeSameAs(store);
        attachedTimeout.ShouldBe(expected);
        Should.NotThrow(() => { _ = new TaskCompletionSource().Task.WaitAsync(attachedTimeout, new FakeTimeProvider()); });
    }

    [Fact]
    public async Task Dispose_FromReducer_MiddlewareDisposedAfterDrainExit()
    {
        // A synchronous reducer can only call the sync Dispose(), which discards the disposal task (§6.11): the ordering
        // still comes from DisposeAsync.
        var logger = new FakeLogger();
        var journal = new List<string>();
        var first = new Recorder("first", journal);
        var last = new Recorder("last", journal);
        var drainExitedAtDispose = new List<bool>();
        var slice = new CountSlice();
        var store = new DuckyStore([slice], logger, middleware: () => [first, last]);
        first.OnDispose = last.OnDispose = () =>
        {
            drainExitedAtDispose.Add(store.Dispatcher.DrainExited!.IsCompleted);
            return default;
        };
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        journal.Clear();
        slice.OnProbe = () =>
        {
            store.Dispose();
            journal.Add("disposal requested");
        };

        store.Dispatch(new Probe());
        await store.DisposeAsync(); // the one disposal task Dispose() started

        // The action being processed still reaches AfterReduce; the middleware is disposed once the drain has exited, in
        // reverse registration order.
        journal.ShouldBe([
            "first.MayDispatch Probe", "last.MayDispatch Probe", "first.BeforeReduce Probe", "last.BeforeReduce Probe",
            "disposal requested", "first.AfterReduce Probe", "last.AfterReduce Probe",
            "last.DisposeAsync", "first.DisposeAsync",
        ]);
        drainExitedAtDispose.ShouldBe([true, true]);
        var warning = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        warning.Id.Id.ShouldBe(1010);
        warning.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task Dispose_DrainTimeout_MiddlewareDisposedAfterDrainExit()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var journal = new List<string>();
        var first = new Recorder("first", journal);
        var last = new Recorder("last", journal);
        var slice = new CountSlice();
        var store = new DuckyStore([slice], logger, disposeTimeout: TimeSpan.FromSeconds(5), timeProvider: time, middleware: () => [first, last]);
        var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.OnDispose = () =>
        {
            disposed.SetResult(store.Dispatcher.DrainExited!.IsCompleted);
            return default;
        };
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        journal.Clear();
        Task? disposal = null;
        var completedAtBound = false;
        slice.OnProbe = () =>
        {
            disposal = store.DisposeAsync().AsTask();
            time.Advance(TimeSpan.FromSeconds(5));
            completedAtBound = disposal.IsCompleted;
            journal.Add("bound reached");
        };

        store.Dispatch(new Probe());

        // DisposeAsync completed at the bound, while the drain was still in flight; phase 5a was chained on the drain's
        // exit, so the middleware is disposed only after it, still in reverse registration order.
        completedAtBound.ShouldBeTrue();
        await disposal.ShouldNotBeNull();
        (await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).ShouldBeTrue();
        journal.SkipWhile(e => e != "bound reached").ShouldBe([
            "bound reached", "first.AfterReduce Probe", "last.AfterReduce Probe", "last.DisposeAsync", "first.DisposeAsync",
        ]);
        logger.Collector.GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1011);
    }

    [Fact]
    public async Task Dispose_FromDrainer_DoesNotDeadlock()
    {
        var journal = new List<string>();
        var recorder = new Recorder("r", journal);
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance, middleware: () => [recorder]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        Task? disposal = null;
        var completedOnDrainer = true;
        recorder.OnAfter = c =>
        {
            if (c.Action is Bump)
            {
                disposal = store.DisposeAsync().AsTask();
                completedOnDrainer = disposal.IsCompleted;
            }
        };

        store.Dispatch(new Bump());

        // The drainer got an incomplete task instead of waiting for its own exit, and disposal ended after it returned.
        completedOnDrainer.ShouldBeFalse();
        await disposal.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        journal.Last().ShouldBe("r.DisposeAsync");
    }

    [Fact]
    public async Task Dispose_MiddlewareDisposeThrows_OthersStillDisposed()
    {
        var logger = new FakeLogger();
        var journal = new List<string>();
        var syncThrow = new InvalidOperationException("sync");
        var asyncThrow = new InvalidOperationException("async");
        var first = new Recorder("first", journal) { OnDispose = () => throw syncThrow };
        var second = new Recorder("second", journal) { OnDispose = () => ValueTask.FromException(asyncThrow) };
        var third = new Recorder("third", journal);
        var store = new DuckyStore([new CountSlice()], logger, middleware: () => [first, second, third]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        journal.Clear();

        var disposal = store.DisposeAsync().AsTask();
        await disposal;

        disposal.IsCompletedSuccessfully.ShouldBeTrue();
        journal.ShouldBe(["third.DisposeAsync", "second.DisposeAsync", "first.DisposeAsync"]);
        var records = logger.Collector.GetSnapshot();
        records.Count.ShouldBe(2);
        records.ShouldAllBe(r => r.Id.Id == 1015 && r.Level == LogLevel.Error);
        records[0].Exception.ShouldBeSameAs(asyncThrow);
        records[1].Exception.ShouldBeSameAs(syncThrow);
        records[0].Message.ShouldBe($"Disposing {typeof(Recorder)} threw");
    }

    [Fact]
    public async Task Dispose_ThrowingLifetimeCallback_CompletesAndDisposesMiddleware()
    {
        var logger = new FakeLogger();
        var journal = new List<string>();
        var recorder = new Recorder("r", journal);
        var store = new DuckyStore([new CountSlice()], logger, middleware: () => [recorder]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.Lifetime.Register(() => throw new InvalidOperationException("callback"));

        await store.DisposeAsync();

        journal.Last().ShouldBe("r.DisposeAsync");
        logger.Collector.GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1013);
    }

    // Non-normative: middleware is created by the first drain; a store that never processed an action creates none, so
    // phase 5a disposes nothing.
    [Fact]
    public async Task Dispose_NothingProcessed_CreatesNoMiddleware()
    {
        var created = 0;
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance, middleware: () =>
        {
            created++;
            return [new Recorder("r", [])];
        });

        await store.DisposeAsync();

        created.ShouldBe(0);
    }

    // Non-normative: phase 5a awaits each middleware's DisposeAsync before starting the next, and DisposeAsync completes
    // only once the phase has finished (§6.11 5a).
    [Fact]
    public async Task Dispose_MiddlewareDisposePending_NextDisposedOnlyAfterItCompletes()
    {
        var journal = new List<string>();
        var lastDisposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new Recorder("first", journal);
        var last = new Recorder("last", journal)
        {
            OnDispose = () =>
            {
                lastDisposing.SetResult();
                return new(release.Task);
            },
        };
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance, middleware: () => [first, last]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        journal.Clear();

        var disposal = store.DisposeAsync().AsTask();
        await lastDisposing.Task;

        journal.ShouldBe(["last.DisposeAsync"]);
        disposal.IsCompleted.ShouldBeFalse();

        release.SetResult();
        await disposal;
        journal.ShouldBe(["last.DisposeAsync", "first.DisposeAsync"]);
    }

    private static void ThrowIf(bool condition, Exception exception)
    {
        if (condition)
        {
            throw exception;
        }
    }
}
