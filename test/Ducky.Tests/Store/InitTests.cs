using Ducky.Tests.InitFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §6.3 (init buffer, MarkReady), §6.7 (init lifecycle), §5.2 (Restore); INV-04, INV-13. An InitGate stands in for
// middleware init, so the store stays before Ready until the test releases it.
public sealed class InitTests
{
    private static string[] Steps(DuckyStore store) => [.. store.State.Get<Trail>().Steps];

    [Fact]
    public async Task Init_AutoStartsOnFirstDispatch()
    {
        // Dispatch: registry reads start nothing; the first dispatch starts init, before Ready its action is buffered.
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, inits: [gate.Init]);
        _ = store.Slices;
        _ = store.InitialState;
        gate.Started.ShouldBe(0);
        store.Dispatch(new Mark("a"));
        gate.Started.ShouldBe(1);
        Steps(store).ShouldBeEmpty();
        gate.Release();
        Steps(store).ShouldBe(["init", "a"]);

        // DispatchAsync.
        gate = new InitGate();
        store = new DuckyStore([new TrailSlice()], NullLogger.Instance, inits: [gate.Init]);
        var dispatched = store.DispatchAsync(new Mark("b"));
        gate.Started.ShouldBe(1);
        dispatched.IsCompleted.ShouldBeFalse();
        gate.Release();
        (await dispatched).ShouldBe(DispatchResult.Reduced);
        Steps(store).ShouldBe(["init", "b"]);

        // A State read: with no middleware, init completes synchronously, so the read already sees StoreInitialized.
        store = new DuckyStore([new TrailSlice()], NullLogger.Instance);
        Steps(store).ShouldBe(["init"]);

        // InitializeAsync.
        gate = new InitGate();
        store = new DuckyStore([new TrailSlice()], NullLogger.Instance, inits: [gate.Init]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        gate.Started.ShouldBe(1);
        gate.Release();
        await initialized;
        Steps(store).ShouldBe(["init"]);
    }

    [Fact]
    public async Task Init_StoreInitializedExactlyOnce()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, inits: [gate.Init]);

        // Every trigger while init runs, and again once it completed: init starts once, StoreInitialized is reduced once.
        var first = store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispatch(new Mark("a"));
        var second = store.InitializeAsync(TestContext.Current.CancellationToken);
        _ = store.State;
        gate.Release();
        await first;
        await second;
        store.Dispatch(new Mark("b"));
        (await store.DispatchAsync(new Mark("c"))).ShouldBe(DispatchResult.Reduced);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        gate.Started.ShouldBe(1);
        Steps(store).ShouldBe(["init", "a", "b", "c"]);
    }

    [Fact]
    public void Init_InitThrowsSynchronously_StoreStillReady()
    {
        // §6.7 step 1: a synchronous throw counts as a finished init, like a faulted task; the next init still starts.
        var started = 0;
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance,
            inits: [() => throw new InvalidOperationException("sync"), () => { started++; return Task.CompletedTask; }]);

        store.Dispatch(new Mark("a"));

        started.ShouldBe(1);
        Steps(store).ShouldBe(["init", "a"]);
    }

    [Fact]
    public async Task PreInitDispatches_AreBufferedAndReplayedInOrder()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, inits: [gate.Init]);

        // Local and Effect origins are both buffered until Ready, in their enqueue order.
        store.Dispatch(new Mark("a"));
        var b = store.DispatchAsync(new Mark("b"));
        store.Dispatcher.Enqueue(store.Dispatcher.NewPending(new Mark("effect"), Origin.Effect, null));
        var d = store.DispatchAsync(new Mark("d"));
        Steps(store).ShouldBeEmpty();
        b.IsCompleted.ShouldBeFalse();

        gate.Release();

        (await b).ShouldBe(DispatchResult.Reduced);
        (await d).ShouldBe(DispatchResult.Reduced);
        Steps(store).ShouldBe(["init", "a", "b", "effect", "d"]);
    }

    [Fact]
    public async Task Restore_BypassesInitBuffer()
    {
        var gate = new InitGate();
        var trail = new TrailSlice();
        var store = new DuckyStore([trail, new OtherSlice()], NullLogger.Instance, inits: [gate.Init]);
        store.Dispatch(new Mark("a"));

        // Before Ready, a restore is reduced at once, ahead of StoreInitialized and the buffered user action.
        store.Restore(new Dictionary<string, object> { [trail.Key] = new Trail(["restored"]) }, Origin.Hydration);
        Steps(store).ShouldBe(["restored"]);
        store.State.WasRestored<Trail>().ShouldBeTrue();
        store.State.WasRestored<Other>().ShouldBeFalse();

        gate.Release();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        Steps(store).ShouldBe(["restored", "init", "a"]);

        // Restore never starts init: a store that only restored is still before Ready.
        gate = new InitGate();
        store = new DuckyStore([new TrailSlice(), new OtherSlice()], NullLogger.Instance, inits: [gate.Init]);
        store.Restore(new Dictionary<string, object> { ["other"] = new Other(7) }, Origin.CrossTab);
        gate.Started.ShouldBe(0);
        store.State.Get<Other>().Value.ShouldBe(7);
        gate.Started.ShouldBe(1);
    }

    [Fact]
    public async Task InitBuffer_ActionsAfterReady_FollowBufferedActions()
    {
        var gate = new InitGate();
        var trail = new TrailSlice();
        var store = new DuckyStore([trail], NullLogger.Instance, inits: [gate.Init]);
        Task<DispatchResult>? fromInit = null;
        Cause? initScope = null;

        // Dispatched while StoreInitialized is reduced, so after Ready: it follows the buffered actions.
        trail.OnInit = () =>
        {
            initScope = store.Dispatcher.Causal;
            fromInit = store.DispatchAsync(new Mark("after-ready"));
        };
        store.Dispatch(new Mark("a"));
        store.Dispatch(new Mark("b"));

        gate.Release();
        (await store.DispatchAsync(new Mark("later"))).ShouldBe(DispatchResult.Reduced);

        (await fromInit.ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        Steps(store).ShouldBe(["init", "a", "b", "after-ready", "later"]);

        // StoreInitialized took the next Id at MarkReady, after the buffered a (1) and b (2), on a chain of its own.
        initScope.ShouldBe(new Cause(3, 0, 3, false));
    }

    // Non-normative: MarkReady while another drain is active only queues; the shared init task completes when
    // StoreInitialized is processed, never from MarkReady (§6.7, the deterministic shape of
    // InitializeAsync_WithConcurrentDrainer_CompletesAfterStoreInitializedReduced).
    [Fact]
    public async Task InitializeAsync_MarkReadyDuringDrain_CompletesAfterStoreInitializedReduced()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice(), new OtherSlice()], NullLogger.Instance, inits: [gate.Init]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        var completedAtMarkReady = true;
        store.Dispatcher.BeforeProcessHook = _ =>
        {
            store.Dispatcher.BeforeProcessHook = null;
            gate.Release();
            completedAtMarkReady = initialized.IsCompleted;
        };

        // The restore's drain is active when the gate releases, so MarkReady only queues StoreInitialized behind it.
        store.Restore(new Dictionary<string, object> { ["other"] = new Other(1) }, Origin.DevTools);

        completedAtMarkReady.ShouldBeFalse();
        await initialized;
        Steps(store).ShouldBe(["init"]);
        store.State.Get<Other>().Value.ShouldBe(1);
    }

    // Non-normative: the caller's token cancels only that caller's wait, never init (§5.2).
    [Fact]
    public async Task InitializeAsync_CallerTokenCancels_OnlyThatWait()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, inits: [gate.Init]);
        using var cts = new CancellationTokenSource();
        var cancelled = store.InitializeAsync(cts.Token);
        var other = store.InitializeAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(cancelled);
        other.IsCompleted.ShouldBeFalse();
        gate.Release();
        await other;
        Steps(store).ShouldBe(["init"]);
    }

    // Non-normative: WasRestored is set only by Hydration; CrossTab and DevTools restore the value without the flag.
    [Fact]
    public void Restore_OnlyHydrationSetsWasRestored()
    {
        var store = new DuckyStore([new TrailSlice(), new OtherSlice()], NullLogger.Instance);

        store.Restore(new Dictionary<string, object> { ["other"] = new Other(1) }, Origin.CrossTab);
        store.Restore(new Dictionary<string, object> { ["other"] = new Other(2) }, Origin.DevTools);

        store.State.Get<Other>().Value.ShouldBe(2);
        store.State.WasRestored<Other>().ShouldBeFalse();
        store.Restore(new Dictionary<string, object> { ["other"] = new Other(3) }, Origin.Hydration);
        store.State.WasRestored<Other>().ShouldBeTrue();

        // The first State read reduced StoreInitialized into the trail; no restore carried that over.
        store.State.WasRestored<Trail>().ShouldBeFalse();
    }

    // Non-normative: Local, Effect and System are not restore origins (programmer error, §5.2); nothing is enqueued.
    [Fact]
    public void Restore_NonRestoreOrigin_Throws()
    {
        var store = new DuckyStore([new TrailSlice(), new OtherSlice()], NullLogger.Instance);
        var values = new Dictionary<string, object> { ["other"] = new Other(1) };

        foreach (var origin in new[] { Origin.Local, Origin.Effect, Origin.System, (Origin)42 })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => store.Restore(values, origin)).ParamName.ShouldBe("origin");
        }

        Should.Throw<ArgumentNullException>(() => store.Restore(null!, Origin.Hydration)).ParamName.ShouldBe("values");
        store.State.Get<Other>().Value.ShouldBe(0);
    }

    // Non-normative: an unknown key or a value that is not the slice's state type restores nothing for that entry; the
    // rest of the batch still restores. The values are copied at the call, so a later change to the dictionary is ignored.
    [Fact]
    public async Task Restore_UnknownKeyOrWrongType_SkippedOthersRestored()
    {
        var gate = new InitGate();
        var trail = new TrailSlice();
        var store = new DuckyStore([trail, new OtherSlice()], NullLogger.Instance, inits: [gate.Init]);
        _ = store.InitializeAsync(TestContext.Current.CancellationToken);
        var values = new Dictionary<string, object>
        {
            ["missing"] = new Trail(["ghost"]),
            [trail.Key] = new Other(9),
            ["other"] = new Other(5),
        };
        store.Dispatcher.BeforeProcessHook = _ => values["other"] = new Other(6);

        store.Restore(values, Origin.Hydration);

        store.Dispatcher.BeforeProcessHook = null;
        store.State.Get<Other>().Value.ShouldBe(5);
        store.State.WasRestored<Other>().ShouldBeTrue();
        store.State.WasRestored<Trail>().ShouldBeFalse();
        Steps(store).ShouldBeEmpty();
        gate.Release();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
    }
}
