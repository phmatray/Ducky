using CsCheck;
using Ducky.Tests.ContractFixtures;
using Ducky.Tests.InitFixtures;
using Ducky.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests.Contract;

// SPEC §7.4, §8.1, INV-10. The never-fault set of the core: IDispatcher.DispatchAsync, EffectContext.DispatchAsync,
// IStore.DisposeAsync, and InitializeAsync/WhenIdleAsync with a non-cancelled token (SliceStore.SetAsync arrives with
// SliceStore, M9-01b; DuckyComponent.DispatchAsync with Ducky.Blazor). Their tasks never fault or cancel under any failure
// injection; the §7.4 programmer errors are thrown synchronously and are not faults, so every argument is non-null and
// the configuration constructible. The first carve-out: only the cancelled caller's own wait completes Canceled.
public sealed class NeverFaultTests
{
    private const string DispatchAsync = "IDispatcher.DispatchAsync";
    private const string EffectContextDispatchAsync = "EffectContext.DispatchAsync";
    private const string DisposeAsync = "IStore.DisposeAsync";
    private const string InitializeAsync = "IStore.InitializeAsync";
    private const string WhenIdleAsync = "IStore.WhenIdleAsync";

    // A Fault for each of the three Fault-valued injections, and a coin for each of the five others.
    private static readonly Gen<Faults> _faults =
        Gen.Select(Gen.Bool.Array[5], Gen.Enum<Fault>().Array[3])
            .Select((flags, modes) => new Faults(flags[0], flags[1], flags[2], flags[3], flags[4], modes[0], modes[1], modes[2]));

    [Fact]
    public void Api_AllAsyncMembers_NeverFault()
    {
        Property.Check(_faults, faults =>
        {
            var tasks = Exercise(faults).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)
                .GetAwaiter().GetResult();
            tasks.Keys.ShouldBe([DispatchAsync, EffectContextDispatchAsync, DisposeAsync, InitializeAsync, WhenIdleAsync], ignoreOrder: true);
            foreach (var (member, calls) in tasks)
            {
                calls.ShouldAllBe(t => t.IsCompletedSuccessfully, $"{member} under {faults}");
            }
        });
    }

    [Fact]
    public Task Api_DispatchAsync_NeverFaults() => NeverFaults(DispatchAsync);

    [Fact]
    public Task Api_EffectContextDispatchAsync_NeverFaults() => NeverFaults(EffectContextDispatchAsync);

    [Fact]
    public Task Api_DisposeAsync_NeverFaults() => NeverFaults(DisposeAsync);

    [Fact]
    public Task Api_InitializeAsync_NeverFaults() => NeverFaults(InitializeAsync);

    [Fact]
    public Task Api_WhenIdleAsync_NeverFaults() => NeverFaults(WhenIdleAsync);

    // Init hangs on a gate. Each cancelled caller (cancelled while waiting, or before the call) gets Canceled; init's own
    // token, the other waiters and a later dispatch are unaffected, and both uncancelled waits complete once init does.
    [Fact]
    public async Task Api_CallerTokenCancelled_OnlyThatWaitCanceled()
    {
        var gate = new InitGate();
        await using var store = new DuckyStore([new JournalSlice()], NullLogger.Instance, middleware: () => [gate]);
        using var caller = new CancellationTokenSource();
        var cancelledInit = store.InitializeAsync(caller.Token);
        var cancelledIdle = store.WhenIdleAsync(caller.Token);
        var init = store.InitializeAsync(TestContext.Current.CancellationToken);
        var idle = store.WhenIdleAsync(TestContext.Current.CancellationToken);

        await caller.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(cancelledInit);
        await Should.ThrowAsync<OperationCanceledException>(cancelledIdle);
        cancelledInit.IsCanceled.ShouldBeTrue();
        cancelledIdle.IsCanceled.ShouldBeTrue();
        store.InitializeAsync(caller.Token).IsCanceled.ShouldBeTrue();
        store.WhenIdleAsync(caller.Token).IsCanceled.ShouldBeTrue();
        gate.Token.IsCancellationRequested.ShouldBeFalse();
        gate.Started.ShouldBe(1);
        init.IsCompleted.ShouldBeFalse();
        idle.IsCompleted.ShouldBeFalse();
        var dispatched = store.DispatchAsync(new Go(1));

        gate.Release();

        await init;
        await idle;
        (await dispatched).ShouldBe(DispatchResult.Reduced);
        store.State.Get<Journal>().Actions.ShouldBe([new StoreInitialized(), new Go(1)]);
    }

    // Every member under the two exhaustive injections: everything throwing synchronously, then asynchronously.
    private static async Task NeverFaults(string member)
    {
        foreach (var faults in new[] { Faults.All, Faults.AllAsync })
        {
            var tasks = await Exercise(faults).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            tasks[member].ShouldNotBeEmpty();
            tasks[member].ShouldAllBe(t => t.IsCompletedSuccessfully, $"{member} under {faults}");
        }
    }

    // One store's life under the injection: init, a dispatch whose effect dispatches through its context, an idle wait,
    // then dispose and every member again once disposal began. Every task is awaited first: a fault or a cancellation
    // surfaces here as the test's own exception.
    private static async Task<Dictionary<string, List<Task>>> Exercise(Faults faults)
    {
        var effect = new FaultyEffect(faults.Effect);
        var store = new DuckyStore(
            [new JournalSlice { ThrowsGo = faults.Reducer, ThrowsOnInit = faults.InitReducer }],
            NullLogger.Instance,
            middleware: () => [new FaultyMiddleware(faults)],
            effects: () => [(effect, false)]);
        Dictionary<string, List<Task>> tasks = new()
        {
            [DispatchAsync] = [],
            [EffectContextDispatchAsync] = [],
            [DisposeAsync] = [],
            [InitializeAsync] = [],
            [WhenIdleAsync] = [],
        };

        tasks[InitializeAsync].Add(store.InitializeAsync(TestContext.Current.CancellationToken));
        tasks[DispatchAsync].Add(store.DispatchAsync(new Go(1)));
        tasks[DispatchAsync].Add(store.DispatchAsync(new Go(3)));
        tasks[WhenIdleAsync].Add(store.WhenIdleAsync(TestContext.Current.CancellationToken));
        await Task.WhenAll(tasks.Values.SelectMany(t => t));

        tasks[DisposeAsync].Add(store.DisposeAsync().AsTask());
        tasks[DispatchAsync].Add(store.DispatchAsync(new Go(2)));
        tasks[InitializeAsync].Add(store.InitializeAsync(TestContext.Current.CancellationToken));
        tasks[WhenIdleAsync].Add(store.WhenIdleAsync(TestContext.Current.CancellationToken));
        tasks[DisposeAsync].Add(store.DisposeAsync().AsTask());
        tasks[EffectContextDispatchAsync].AddRange(effect.Dispatched);
        await Task.WhenAll(tasks.Values.SelectMany(t => t));
        return tasks;
    }
}
