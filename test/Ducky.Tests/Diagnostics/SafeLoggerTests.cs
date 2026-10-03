using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.InitFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests.Diagnostics;

// SPEC §6.3 (SafeLogger), §6.7 and §6.11 (the lifecycle paths outside the drain); INV-03, INV-13, INV-29.
public sealed class SafeLoggerTests
{
    [Fact]
    public async Task SafeLogger_ProviderThrows_NeverEscapes()
    {
        // Every non-fatal throw is swallowed: IsEnabled then says false and BeginScope returns a no-op scope.
        var failing = new ThrowingLogger(_ => new InvalidOperationException("log"))
        {
            OnIsEnabled = new AggregateException("enabled"),
            OnBeginScope = new InvalidOperationException("scope"),
        };
        var safe = new SafeLogger(failing);
#pragma warning disable CA1848 // justification: the test calls ILogger.Log directly, as a provider sees it
        safe.LogWarning(new EventId(7), "swallowed");
#pragma warning restore CA1848
        failing.Logged.ShouldBe([7]);
        safe.IsEnabled(LogLevel.Error).ShouldBeFalse();
        safe.BeginScope("scope").ShouldNotBeNull().Dispose();

        // OutOfMemoryException is fatal (§10): it passes through every member.
        var fatal = new ThrowingLogger(_ => ThrowingLogger.Fatal())
        {
            OnIsEnabled = ThrowingLogger.Fatal(),
            OnBeginScope = ThrowingLogger.Fatal(),
        };
        var unsafeFatal = new SafeLogger(fatal);
#pragma warning disable CA1848 // justification: as above
        Should.Throw<OutOfMemoryException>(() => unsafeFatal.LogWarning(new EventId(8), "fatal"));
#pragma warning restore CA1848
        Should.Throw<OutOfMemoryException>(() => unsafeFatal.IsEnabled(LogLevel.Error));
        Should.Throw<OutOfMemoryException>(() => unsafeFatal.BeginScope("scope"));

        // A healthy provider sees every call unchanged.
        var fake = new FakeLogger();
        var error = new InvalidOperationException("payload");
#pragma warning disable CA1848 // justification: as above
        new SafeLogger(fake).LogError(new EventId(9), error, "forwarded {Value}", 42);
#pragma warning restore CA1848
        var record = fake.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(9);
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeSameAs(error);
        record.Message.ShouldBe("forwarded 42");
        var scope = new MemoryStream();
        var healthy = new SafeLogger(new ThrowingLogger(_ => null) { Enabled = false, Scope = scope });
        healthy.IsEnabled(LogLevel.Error).ShouldBeFalse();
        new SafeLogger(new ThrowingLogger(_ => null)).IsEnabled(LogLevel.Error).ShouldBeTrue();
        healthy.BeginScope("scope").ShouldBeSameAs(scope);

        // The store wraps its logger once, at construction: the reducer's EventId 1000 log throws inside the provider
        // and nothing escapes Process, so the catch-all (EventId 1012) never runs.
        var provider = new ThrowingLogger(_ => new InvalidOperationException("provider"));
        var store = new DuckyStore([new CountSlice(), new BoomSlice()], provider);
        (await store.DispatchAsync(new Boom())).ShouldBe(DispatchResult.Failed);
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
        provider.Logged.ShouldBe([1000]);
    }

    [Fact]
    public async Task Lifecycle_ThrowingLoggerProvider_StoreReadyAndDisposeComplete()
    {
        // Every lifecycle log goes through SafeLogger, so a provider that throws on each call skips nothing: a faulted
        // middleware init (1032) and a timer abort (1031) still reach Ready, a hung (1016) and a throwing (1015) middleware
        // DisposeAsync still let DisposeAsync complete, a Dispatch after dispose (1002) does not throw, and an overflow
        // abort (1031) still reaches Ready.
        var time = new FakeTimeProvider();
        var provider = new ThrowingLogger(_ => new InvalidOperationException("provider"));
        var throwing = new Recorder("throwing", []) { OnDispose = () => throw new InvalidOperationException("dispose") };
        var faulted = new Recorder("faulted", []) { OnInit = _ => ValueTask.FromException(new InvalidOperationException("init")) };
        var waiting = new Recorder("waiting", []) { OnInit = t => new(Task.Delay(Timeout.InfiniteTimeSpan, time, t)) };
        var hung = new Recorder("hung", []) { OnDispose = () => new(new TaskCompletionSource().Task) };
        var store = new DuckyStore([new TrailSlice()], provider, initTimeout: TimeSpan.FromSeconds(1),
            disposeTimeout: TimeSpan.FromSeconds(1), timeProvider: time, middleware: () => [throwing, faulted, waiting, hung]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);

        time.Advance(TimeSpan.FromSeconds(1));

        await initialized.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        store.State.Get<Trail>().Steps.ShouldBe(["init"]);
        var disposal = store.DisposeAsync().AsTask();
        time.Advance(TimeSpan.FromSeconds(1));
        await disposal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        disposal.IsCompletedSuccessfully.ShouldBeTrue();
        Should.NotThrow(() => store.Dispatch(new Mark("late")));
        provider.Logged.ShouldBe([1032, 1031, 1016, 1015, 1002]);

        // An overflow abort (one abort wins per store, so a second store): its 1031 throws too, and the store still reaches
        // Ready with every buffered action, then disposes past the init that never ends.
        var overflowTime = new FakeTimeProvider();
        var overflowProvider = new ThrowingLogger(_ => new InvalidOperationException("provider"));
        var overflowed = new DuckyStore([new TrailSlice()], overflowProvider, disposeTimeout: TimeSpan.FromSeconds(1),
            timeProvider: overflowTime, initBufferCapacity: 1,
            middleware: () => [new InitProbe((_, _) => new(new TaskCompletionSource().Task))]);
        List<Action> queued = [];
        overflowed.Dispatcher.QueueWorkItem = queued.Add;
        overflowed.Dispatch(new Mark("a"));
        overflowed.Dispatch(new Mark("b"));

        queued.ShouldHaveSingleItem()();

        overflowed.State.Get<Trail>().Steps.ShouldBe(["init", "a", "b"]);
        var overflowDisposal = overflowed.DisposeAsync().AsTask();
        overflowTime.Advance(TimeSpan.FromSeconds(1));
        await overflowDisposal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        overflowDisposal.IsCompletedSuccessfully.ShouldBeTrue();
        overflowProvider.Logged.ShouldBe([1031]);
    }
}
