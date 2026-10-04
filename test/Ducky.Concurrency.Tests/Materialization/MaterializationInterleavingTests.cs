using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Concurrency.Tests;

// SPEC §6.6 materialization against concurrent first users (INV-05 carve-out) and against disposal (INV-29). Every party
// that blocks runs on a dedicated thread, so a cold 2-vCPU pool can't starve the choreography.
[Collection(nameof(Interleaving))]
public sealed class MaterializationInterleavingTests
{
    private static Task Dedicated(Action action, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    // Progress-bounded by the caller's Interleaving.Within, never by a wall clock of its own.
    private static async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1), TimeProvider.System, cancellationToken);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_gate")]
    private static extern ref Lock Gate(Dispatcher dispatcher);

    // Two first users race on dedicated threads while the one materialization is held in a store-owned effect's
    // constructor. The gate opens only once the caller that is not constructing is blocked (on the Lazy, the next block
    // past the barrier), so it provably waited: the factory runs once, neither caller returns before it ended, and both
    // actions reduce.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Materialization_ConcurrentFirstUse_SecondCallerWaitsThenSucceeds(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var constructing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var built = 0;
        Thread? constructor = null;
        var log = new LogSlice();
        var store = new DuckyStore(
            [log],
            NullLogger.Instance,
            effects: () =>
            [
                (new GatedEffect(() =>
                {
                    Interlocked.Increment(ref built);
                    Volatile.Write(ref constructor, Thread.CurrentThread);
                    constructing.SetResult();
                    release.Task.Wait(cancellationToken);
                }), true),
            ]);
        using var barrier = new Barrier(2);
        var threads = new Thread[2];
        var pastBarrier = new bool[2];
        var releasedAtReturn = new bool[2];
        var callers = Enumerable.Range(0, 2).Select(p => Dedicated(
            () =>
            {
                threads[p] = Thread.CurrentThread;
                barrier.SignalAndWait(cancellationToken);
                Volatile.Write(ref pastBarrier[p], true);
                store.Dispatch(new Add(p, 0));
                releasedAtReturn[p] = release.Task.IsCompleted;
            },
            cancellationToken)).ToArray();

        await Interleaving.Within(constructing.Task);
        var waiter = threads[0] == Volatile.Read(ref constructor) ? 1 : 0;
        await Interleaving.Within(UntilAsync(
            () => Volatile.Read(ref pastBarrier[waiter]) && threads[waiter].ThreadState.HasFlag(ThreadState.WaitSleepJoin),
            cancellationToken));
        release.SetResult();
        await Interleaving.Within(Task.WhenAll(callers));

        built.ShouldBe(1);
        releasedAtReturn.ShouldAllBe(released => released);
        await Interleaving.Settled(store);
        store.State.Get<Log>().Entries.Count.ShouldBe(2);
        await Interleaving.Within(store.DisposeAsync().AsTask());
    }

    // A first use and a disposal on dedicated threads, for a Singleton store whose effect and middleware resolve a
    // transient dependency from the store scope. Even repeats hold the middleware constructor until DisposeAsync was
    // called (disposal awaits the in-flight factory); odd ones race freely (materialization never started, in flight, or
    // done). Every constructed effect and middleware is disposed exactly once, construction is all or nothing, and no
    // constructor ends with a dependency the store scope already disposed: that scope outlives every resolution.
    // DisposeTimeout is infinite, so only that rule, never a bound, can complete the disposal.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Dispose_ConcurrentWithFirstUse_EveryConstructedMiddlewareDisposed(int repeat)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var hold = repeat % 2 == 0;
        var journal = new Journal(cancellationToken);
        if (!hold)
        {
            journal.Release.SetResult();
        }

        await using var provider = new ServiceCollection()
            .AddSingleton(journal)
            .AddTransient<Dependency>()
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Singleton;
                d.DisposeTimeout = Timeout.InfiniteTimeSpan;
                d.AddSlice<LogSlice>();
                d.AddEffect<ProbeEffect>();
                d.Use<GatedMiddleware>();
                d.Use<LastMiddleware>();
            })
            .BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();
        using var barrier = new Barrier(2);
        Task disposal = null!;
        var completedWhileHeld = false;
        var user = Dedicated(
            () =>
            {
                barrier.SignalAndWait(cancellationToken);
                store.Dispatch(new Add(0, 0));
            },
            cancellationToken);
        var disposer = Dedicated(
            () =>
            {
                barrier.SignalAndWait(cancellationToken);
                if (hold)
                {
                    Interleaving.Wait(journal.Constructing.Task);
                }

                disposal = store.DisposeAsync().AsTask();
                if (hold)
                {
                    completedWhileHeld = disposal.IsCompleted;
                    journal.Release.SetResult();
                }
            },
            cancellationToken);

        await Interleaving.Within(Task.WhenAll(user, disposer));
        await Interleaving.Within(disposal);

        string[] all = [nameof(ProbeEffect), nameof(GatedMiddleware), nameof(LastMiddleware)];
        var constructed = journal.Constructed.ToArray();
        if (hold)
        {
            completedWhileHeld.ShouldBeFalse();
            constructed.ShouldBe(all);
        }
        else
        {
            constructed.Length.ShouldBeOneOf(0, all.Length);
        }

        var disposed = journal.Disposed.ToArray();
        constructed.ShouldAllBe(name => disposed.Count(d => d == name) == 1);
        disposed.Count(d => d == nameof(Dependency)).ShouldBe(constructed.Length);
        disposed.Length.ShouldBe(2 * constructed.Length);
        journal.DeadAtConstruction.ShouldBe(0);
    }

    // Disposal has published its task but not yet closed the store (step 1 waits for _gate, which the holder thread
    // holds) when a constructor throws: the store is not disposed, so the materializing caller rethrows the cached
    // exception instead of taking a live path with a faulted materialization (§6.6). The holder re-enters its own lock.
    [Fact]
    public async Task Materialization_CtorThrowsBeforeDisposalClosesStore_Rethrown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new DuckyStore([new LogSlice()], NullLogger.Instance, middleware: () => throw new FormatException("ctor"));
        Task disposer = null!;
        Exception? thrown = null;
        var holder = Dedicated(
            () =>
            {
                lock (Gate(store.Dispatcher))
                {
                    disposer = Dedicated(() => _ = store.DisposeAsync().AsTask(), cancellationToken);
                    Interleaving.Wait(UntilAsync(() => store.Dispatcher.DisposalBegan, cancellationToken));
                    thrown = Record.Exception(() => store.Dispatch(new Add(0, 0)));
                }
            },
            cancellationToken);

        await Interleaving.Within(holder);
        await Interleaving.Within(disposer);
        await Interleaving.Within(store.DisposeAsync().AsTask());

        thrown.ShouldBeOfType<FormatException>();
        store.Dispatch(new Add(0, 0));
    }
}

// Its constructor runs the test's action: a held materialization.
internal sealed class GatedEffect : Effect<Add>
{
    public GatedEffect(Action onConstruct) => onConstruct();

    public override Task Handle(Add action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

// What the store-created instances below constructed and disposed, and the gate GatedMiddleware's constructor waits on.
internal sealed class Journal(CancellationToken cancellationToken)
{
    private int _deadAtConstruction;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public TaskCompletionSource Constructing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConcurrentQueue<string> Constructed { get; } = new();

    public ConcurrentQueue<string> Disposed { get; } = new();

    public int DeadAtConstruction => Volatile.Read(ref _deadAtConstruction);

    public void Built(string name, Dependency dependency)
    {
        if (dependency.IsDisposed)
        {
            Interlocked.Increment(ref _deadAtConstruction);
        }

        Constructed.Enqueue(name);
    }
}

// A transient resolved from the store scope, which tracks and disposes it (phase 5b).
internal sealed class Dependency(Journal journal) : IDisposable
{
    private volatile bool _disposed;

    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        _disposed = true;
        journal.Disposed.Enqueue(nameof(Dependency));
    }
}

internal sealed class ProbeEffect : Effect<Add>, IDisposable
{
    private readonly Journal _journal;

    public ProbeEffect(Journal journal, Dependency dependency)
    {
        _journal = journal;
        journal.Built(nameof(ProbeEffect), dependency);
    }

    public override Task Handle(Add action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _journal.Disposed.Enqueue(nameof(ProbeEffect));
}

internal sealed class GatedMiddleware : Middleware
{
    private readonly Journal _journal;

    public GatedMiddleware(Journal journal, Dependency dependency)
    {
        _journal = journal;
        journal.Constructing.TrySetResult();
        journal.Release.Task.Wait(journal.CancellationToken);
        journal.Built(nameof(GatedMiddleware), dependency);
    }

    public override ValueTask DisposeAsync()
    {
        _journal.Disposed.Enqueue(nameof(GatedMiddleware));
        return base.DisposeAsync();
    }
}

internal sealed class LastMiddleware : Middleware
{
    private readonly Journal _journal;

    public LastMiddleware(Journal journal, Dependency dependency)
    {
        _journal = journal;
        journal.Built(nameof(LastMiddleware), dependency);
    }

    public override ValueTask DisposeAsync()
    {
        _journal.Disposed.Enqueue(nameof(LastMiddleware));
        return base.DisposeAsync();
    }
}
