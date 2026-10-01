using Ducky.Tests.Diagnostics;
using Ducky.Tests.DispatcherFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §6.3: the catch-all around Process, reached only through BeforeProcessHook; INV-03.
public sealed class DrainTests
{
    [Fact]
    public async Task Drain_ProcessThrowsUnexpectedly_NextActionStillDrained()
    {
        var logger = new FakeLogger();
        var store = new DuckyStore([new CountSlice()], logger);
        var thrown = new InvalidOperationException("hook");
        var (seen, next) = ThrowOnPing(store, thrown);
        var ping = new Ping();

        (await store.DispatchAsync(ping)).ShouldBe(DispatchResult.Failed);

        (await next().ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        seen.Count.ShouldBe(2);
        seen[0].ShouldBeSameAs(ping);
        seen[1].ShouldBeOfType<Bump>();
        store.State.Get<Count>().Value.ShouldBe(1);
        store.Dispatcher.DrainExited.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1012);
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeSameAs(thrown);
        record.Message.ShouldBe($"unexpected exception while processing {typeof(Ping)}");
        record.StructuredState.ShouldNotBeNull().ShouldContain(kv => kv.Key == "Type" && kv.Value == typeof(Ping).ToString());
    }

    [Fact]
    public async Task Drain_ProcessThrowsAndLogRethrowsFatal_ActionCompletedAndNextDrained()
    {
        // SafeLogger rethrows OutOfMemoryException: only the catch-all's own try/catch keeps it off the drain. Ping is
        // queued while a first Bump is processed, so its task exists when 1012 is logged and pins "complete first".
        Task<DispatchResult>? ping = null;
        bool? completedAtLog = null;
        var logger = new ThrowingLogger(id =>
        {
            if (id.Id != 1012)
            {
                return null;
            }

            completedAtLog = ping!.IsCompleted;
            return ThrowingLogger.Fatal();
        });
        var store = new DuckyStore([new CountSlice()], logger);
        var (_, next) = ThrowOnPing(store, new InvalidOperationException("hook"));
        var throwOnPing = store.Dispatcher.BeforeProcessHook!;
        store.Dispatcher.BeforeProcessHook = action =>
        {
            if (action is Bump && ping is null)
            {
                ping = store.DispatchAsync(new Ping());
            }

            throwOnPing(action);
        };

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);

        (await ping.ShouldNotBeNull()).ShouldBe(DispatchResult.Failed);
        completedAtLog.ShouldBe(true);
        (await next().ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        store.State.Get<Count>().Value.ShouldBe(2);
        store.Dispatcher.DrainExited.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
        logger.Logged.ShouldBe([1012]);
    }

    // On Ping, the hook queues a Bump behind it (the caller is the drainer) and throws.
    private static (List<object> Seen, Func<Task<DispatchResult>?> Next) ThrowOnPing(DuckyStore store, Exception thrown)
    {
        var seen = new List<object>();
        Task<DispatchResult>? next = null;
        store.Dispatcher.BeforeProcessHook = action =>
        {
            seen.Add(action);
            if (action is Ping)
            {
                next = store.DispatchAsync(new Bump());
                throw thrown;
            }
        };
        return (seen, () => next);
    }
}
