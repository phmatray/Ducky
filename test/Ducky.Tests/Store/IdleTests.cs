using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.InitFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §6.3 (idle waiters, no completion under _gate), §6.7 (WhenIdleAsync starts init), §6.11 step 1 (dispose completes
// idle waiters), §7 rule 3; INV-13. Idle is Ready, queue empty, not draining and no non-LongRunning effect run (§6.6,
// QuiescenceTests).
// A wait without a token is the waiters' own task (WaitAsync hands it back), so whether it has completed is observable
// synchronously; with a cancellable token the completion reaches the caller's task only asynchronously.
public sealed class IdleTests
{
    private static string[] Steps(DuckyStore store) => [.. store.State.Get<Trail>().Steps];

    // Non-normative: a wait registered while a drain runs completes once the drain has exited, after everything queued
    // behind it (it is still pending while the last queued action is being reduced); an idle store completes at once,
    // and a completed round never satisfies a later wait.
    [Fact]
    public async Task WhenIdle_CompletesAfterDrainExit()
    {
        var slice = new CountSlice();
        var store = new DuckyStore([slice], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.WhenIdleAsync(TestContext.Current.CancellationToken).IsCompletedSuccessfully.ShouldBeTrue();

        for (var round = 0; round < 2; round++)
        {
            Task? idle = null;
            bool? completedWhileReducingLast = null;
            slice.OnProbe = () =>
            {
                if (idle is null)
                {
                    idle = store.WhenIdleAsync(CancellationToken.None);
                    store.Dispatch(new Probe());
                }
                else
                {
                    completedWhileReducingLast = idle.IsCompleted;
                }
            };

            store.Dispatch(new Probe());

            completedWhileReducingLast.ShouldBe(false);
            idle.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
        }
    }

    // Non-normative: a cancelled token ends only its own caller's wait; init keeps running and every other wait still
    // completes when the store becomes idle.
    [Fact]
    public async Task WhenIdle_CallerTokenCancels_OnlyThatWait()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);
        using var cts = new CancellationTokenSource();
        var other = store.WhenIdleAsync(CancellationToken.None);
        var cancelled = store.WhenIdleAsync(cts.Token);

        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(cancelled);
        other.IsCompleted.ShouldBeFalse();
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        initialized.IsCompleted.ShouldBeFalse();
        gate.Release();

        // The first wait of the round still completes, after a later one joined and was cancelled.
        other.IsCompletedSuccessfully.ShouldBeTrue();
        await initialized.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        gate.Started.ShouldBe(1);
        Steps(store).ShouldBe(["init"]);
    }

    // Non-normative: WhenIdleAsync starts init (INV-13). Before Ready the store is never idle, even when a restore drain
    // exits; the wait completes once StoreInitialized and the replayed buffer have been reduced. With every init
    // synchronous, the first call drains inline and already returns a completed task.
    [Fact]
    public void WhenIdle_StartsInit()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice(), new CountSlice()], NullLogger.Instance, middleware: () => [gate]);

        var idle = store.WhenIdleAsync(CancellationToken.None);

        gate.Started.ShouldBe(1);
        store.Dispatch(new Mark("a"));
        store.Restore(new Dictionary<string, object> { ["count"] = new Count(7) }, Origin.DevTools);
        store.State.Get<Count>().Value.ShouldBe(7);
        idle.IsCompleted.ShouldBeFalse();
        gate.Release();
        idle.IsCompletedSuccessfully.ShouldBeTrue();
        Steps(store).ShouldBe(["init", "a"]);

        store = new DuckyStore([new TrailSlice()], NullLogger.Instance);
        store.WhenIdleAsync(TestContext.Current.CancellationToken).IsCompletedSuccessfully.ShouldBeTrue();
        Steps(store).ShouldBe(["init"]);
    }

    // Non-normative: dispose step 1 completes every idle waiter, before the drain exits and before Ready was ever
    // reached; a wait that starts after disposal completes at once (§6.11).
    [Fact]
    public async Task WhenIdle_Dispose_CompletesWaitersAtStepOne()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);
        var beforeReady = store.WhenIdleAsync(CancellationToken.None);
        var first = store.DisposeAsync().AsTask();
        beforeReady.IsCompletedSuccessfully.ShouldBeTrue();

        // The gate's init ignores its token: phase 5a disposes it, and the disposal completes, once it ends.
        gate.Release();
        await first.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var disposed = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);
        await disposed.DisposeAsync();
        disposed.WhenIdleAsync(TestContext.Current.CancellationToken).IsCompletedSuccessfully.ShouldBeTrue();

        var slice = new CountSlice();
        var drained = new DuckyStore([slice], NullLogger.Instance);
        await drained.InitializeAsync(TestContext.Current.CancellationToken);
        Task? disposal = null;
        var completedInDrain = false;
        slice.OnProbe = () =>
        {
            var idle = drained.WhenIdleAsync(CancellationToken.None);
            disposal = drained.DisposeAsync().AsTask();
            completedInDrain = idle.IsCompletedSuccessfully && !disposal.IsCompleted;
        };

        drained.Dispatch(new Probe());

        completedInDrain.ShouldBeTrue();
        await disposal.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
}
