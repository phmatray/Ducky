using Ducky.Tests.EffectFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests;

// SPEC §6.4 step 11 (no effect starts once disposal began), §6.6 (the run registry _runs, EffectRunToken.DisposeCalled),
// §6.11 step 4 (the run wait) and phase 5b; INV-11, INV-29. The FakeTimeProvider never advances unless a test says so, so
// a disposal that completed did so without reaching its DisposeTimeout bound.
public sealed class EffectDisposeTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(5);

    // Every run is registered, LongRunning included, and step 4 waits for each by its own EffectRunToken: a Merge run that
    // calls DisposeAsync is not waited for, yet the other Merge run, sharing its store-lifetime token, still is. 5a does not
    // wait for runs; 5b (owned effects) runs once every run has ended, whichever ends last. Completing the middleware's
    // DisposeAsync resumes disposal inline, so it has reached the run wait when SetResult returns.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Dispose_AwaitsMergeAndLongRunningRuns(bool mergeEndsFirst)
    {
        DuckyStore store = null!;
        var mergeGate = new TaskCompletionSource();
        var longRunningGate = new TaskCompletionSource();
        var middlewareDisposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var middlewareDisposed = new TaskCompletionSource();
        var middleware = new Recorder("m", [])
        {
            OnDispose = () =>
            {
                middlewareDisposing.SetResult();
                return new(middlewareDisposed.Task);
            },
        };
        var disposing = new Handler<Load>(async (_, _, _) => await store.DisposeAsync().ConfigureAwait(false));
        var merge = new Handler<Load>(async (_, _, _) => await mergeGate.Task.ConfigureAwait(false));
        var longRunning = new LongRunningHandler<Load>(async (_, _, _) => await longRunningGate.Task.ConfigureAwait(false));
        var owned = new DisposableHandler();
        store = new DuckyStore(
            [new SeenSlice()],
            NullLogger.Instance,
            timeProvider: new FakeTimeProvider(),
            middleware: () => [middleware],
            effects: () => [(merge, false), (longRunning, false), (owned, true), (disposing, false)]);
        store.Dispatch(new Load(1));
        var disposal = store.Dispatcher.DisposalSteps.ShouldNotBeNull();
        await middlewareDisposing.Task.WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);

        middlewareDisposed.SetResult();
        (mergeEndsFirst ? mergeGate : longRunningGate).SetResult();

        disposal.IsCompleted.ShouldBeFalse();
        owned.Disposed.ShouldBeFalse();
        (mergeEndsFirst ? longRunningGate : mergeGate).SetResult();
        await disposal.WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        owned.Disposed.ShouldBeTrue();
        owned.Handled.ShouldBe([new Load(1)]);
    }

    // Non-normative (§6.6 "whatever its policy"): a Switch run, on its slot's linked token rather than the store lifetime,
    // is registered like a Merge run, and 5b waits for it even though disposal cancelled its token.
    [Fact]
    public async Task Dispose_AwaitsSwitchRun()
    {
        var gate = new TaskCompletionSource();
        var middlewareDisposed = new TaskCompletionSource();
        var middlewareDisposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var middleware = new Recorder("m", [])
        {
            OnDispose = () =>
            {
                middlewareDisposing.SetResult();
                return new(middlewareDisposed.Task);
            },
        };
        var running = new SwitchHandler<Load>(async (_, _, _) => await gate.Task.ConfigureAwait(false));
        var owned = new DisposableHandler();
        var store = new DuckyStore(
            [new SeenSlice()],
            NullLogger.Instance,
            timeProvider: new FakeTimeProvider(),
            middleware: () => [middleware],
            effects: () => [(running, false), (owned, true)]);
        store.Dispatch(new Load(1));
        store.Dispatcher.RunCount.ShouldBe(1);

        var disposal = store.DisposeAsync().AsTask();
        await middlewareDisposing.Task.WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        middlewareDisposed.SetResult();

        owned.Disposed.ShouldBeFalse();
        gate.SetResult();
        await disposal.WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        owned.Disposed.ShouldBeTrue();
        store.Dispatcher.RunCount.ShouldBe(0);
    }

    // The first call comes from a run after its first await, a second one from another run resumed inline by step 2's
    // cancellation; each completes its own run's DisposeCalled, so disposal waits for neither and completes unbounded.
    [Fact]
    public async Task Dispose_SecondCallFromEffectRun_DoesNotWaitForItself()
    {
        DuckyStore store = null!;
        var gate = new TaskCompletionSource(); // SetResult resumes the first run inline
        Task? first = null;
        Task? second = null;
        var firstCaller = new Handler<Load>(async (_, _, _) =>
        {
            await gate.Task.ConfigureAwait(false);
            first = store.DisposeAsync().AsTask();
            await first.ConfigureAwait(false);
        });
        var secondCaller = new Handler<Load>(async (_, _, token) =>
        {
            var cancelled = new TaskCompletionSource();
            await using var registration = token.Register(cancelled.SetResult);
            await cancelled.Task.ConfigureAwait(false);
            second = store.DisposeAsync().AsTask();
            await second.ConfigureAwait(false);
        });
        var owned = new DisposableHandler();
        store = new DuckyStore(
            [new SeenSlice()],
            NullLogger.Instance,
            timeProvider: new FakeTimeProvider(),
            effects: () => [(firstCaller, false), (secondCaller, false), (owned, true)]);
        store.Dispatch(new Load(1));

        gate.SetResult();

        second.ShouldNotBeNull().ShouldBeSameAs(first.ShouldNotBeNull());
        await first.WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        owned.Disposed.ShouldBeTrue();
    }

    // AfterReduce disposes the store: step 11 sees the cancelled lifetime once, starts no effect for the action and logs
    // Debug 1040; the action itself was reduced.
    [Fact]
    public async Task Dispose_FromAfterReduce_NoEffectStartedForThatAction()
    {
        var logger = new FakeLogger();
        var effect = new DisposableHandler();
        DuckyStore store = null!;
        Task? disposal = null;
        var disposer = new Recorder("m", []) { OnAfter = c => disposal ??= c.Action is Load ? store.DisposeAsync().AsTask() : null };
        store = new DuckyStore([new SeenSlice()], logger, middleware: () => [disposer], effects: () => [(effect, false)]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        logger.Collector.Clear();

        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);

        await disposal.ShouldNotBeNull();
        effect.Handled.ShouldBeEmpty();
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1040);
        record.Level.ShouldBe(LogLevel.Debug);
        record.Message.ShouldBe($"No effect started for {typeof(Load)}: the store is disposing");
    }

    // Non-normative: a run that ignores its token holds only 5b. DisposeAsync completes at the DisposeTimeout bound with
    // middleware already disposed and subscribers cleared (step 6 is not chained on the runs); the owned effects are
    // disposed, chained on the run wait, once the run ends, and the hung run leaves the registry when it ends.
    [Fact]
    public async Task Dispose_HungEffectRun_CompletesAtBoundAndDisposesEffectsAfterRun()
    {
        var time = new FakeTimeProvider();
        List<string> journal = [];
        var hung = new TaskCompletionSource();
        var owned = new DisposableHandler();
        var store = new DuckyStore(
            [new SeenSlice()],
            NullLogger.Instance,
            disposeTimeout: _bound,
            timeProvider: time,
            middleware: () => [new Recorder("m", journal)],
            effects: () => [(new Handler<Load>(async (_, _, _) => await hung.Task.ConfigureAwait(false)), false), (owned, true)]);
        using var selection = store.Select(s => s.Get<Seen>(), _ => { });
        store.Dispatch(new Load(1));

        var disposal = store.DisposeAsync().AsTask();
        journal[^1].ShouldBe("m.DisposeAsync");
        time.Advance(_bound);

        await disposal.WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        store.Dispatcher.HasSubscribers.ShouldBeFalse();
        owned.Disposed.ShouldBeFalse();
        store.Dispatcher.RunCount.ShouldBe(1);
        hung.SetResult(); // ends the run inline, and its continuation removes it from the registry
        store.Dispatcher.RunCount.ShouldBe(0);
        await store.Dispatcher.DisposalSteps.ShouldNotBeNull().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        owned.Disposed.ShouldBeTrue();
    }

    // Non-normative: a Singleton (browser) store owns its store scope and disposes it last in 5b, after its effects, so a
    // scoped dependency outlives the effect that uses it (§6.10, §6.11).
    [Fact]
    public async Task Dispose_SingletonStore_StoreScopeDisposedAfterEffects()
    {
        var journal = new EffectJournal();
        var services = new ServiceCollection()
            .AddSingleton(journal)
            .AddScoped<ScopedProbe>()
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Singleton;
                d.IsBrowser = true;
                d.AddSlice<SeenSlice>();
                d.AddEffect<ScopedEffect>();
            });
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();
        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);

        await store.DisposeAsync();

        journal.Disposed.ShouldBe([nameof(ScopedEffect), nameof(ScopedProbe)]);
    }

    // Non-normative: a Singleton store owns its store scope even when materialization faulted, so 5b still disposes it and
    // the scoped dependency the throwing effect resolved before its constructor threw is released (§6.10, §6.11).
    [Fact]
    public async Task Dispose_SingletonStoreMaterializationFaulted_StoreScopeDisposed()
    {
        var journal = new EffectJournal();
        var services = new ServiceCollection()
            .AddSingleton(journal)
            .AddScoped<ScopedProbe>()
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Singleton;
                d.IsBrowser = true;
                d.AddSlice<SeenSlice>();
                d.AddEffect<ScopedThrowingEffect>();
            });
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();
        Should.Throw<DuckyConfigurationException>(() => store.Dispatch(new Load(1)));

        await store.DisposeAsync();

        journal.Disposed.ShouldBe([nameof(ScopedProbe)]);
    }
}
