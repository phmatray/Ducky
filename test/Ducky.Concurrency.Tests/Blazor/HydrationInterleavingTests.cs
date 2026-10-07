using CsCheck;
using Ducky.Blazor;
using Ducky.ConcurrencyTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Ducky.Concurrency.Tests;

// SPEC §11.5 step 3 and "Before StoreInitialized", §6.7: INV-14. The init-token callback is registered on the init token
// itself, so an init abort waits for _issue before MarkReady even while the init-phase deadline is already ending the
// attempt on another thread.
[Collection(nameof(Interleaving))]
public sealed class HydrationInterleavingTests
{
    private static readonly TimeSpan _hydrationTimeout = TimeSpan.FromSeconds(5);

    // (non-normative) The winner holds _issue in its restore's inline drain (Choreography.Hold). There, the deadline fires
    // on thread T2, which blocks on _issue inside the deadline's cancellation, and then the captured overflow abort runs on
    // T3. Had the abort reached the init token only through a token linked to the deadline, its Cancel() would return at
    // once (the deadline's cancellation is already running on T2), and MarkReady would queue StoreInitialized and the
    // buffered user actions into the winner's drain, ahead of its terminal. Deterministic: both threads are dedicated, and
    // the winner waits, by their progress, until T2 is blocked and T3 is blocked or done.
    [Fact]
    public async Task Hydration_DeadlineEndingAttemptWhenInitAborts_TerminalPrecedesStoreInitialized()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var choreography = new Choreography();
        await using var provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(time)
            .AddSingleton<IJSRuntime>(new Js(read.Task))
            .AddSingleton(choreography)
            .AddDucky(d =>
            {
                d.InitBufferCapacity = 1;
                d.UseJson(ConcurrencyJson.Default).AddSlice<SavedSlice>()
                    .AddBlazor(o => (o.IsBrowser, o.HydrationTimeout) = (true, _hydrationTimeout))
                    .Persist<SavedSlice>();
                d.Use<Choreographer>();
            })
            .BuildServiceProvider();
        var store = (DuckyStore)provider.GetRequiredService<IStore>();
        List<Action> queued = [];
        store.Dispatcher.QueueWorkItem = queued.Add;
        choreography.Hold = () =>
        {
            var deadline = Start(() => time.Advance(_hydrationTimeout));
            Until(() => Blocked(deadline));
            var abort = Start(queued.ShouldHaveSingleItem());
            Until(() => Blocked(abort) || !abort.IsAlive);
            return [deadline, abort];
        };

        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispatch(new Tick());
        store.Dispatch(new Tick());
        read.SetResult(EnvelopeWriter.Write("""{"Value":3}""", version: 1, time.GetUtcNow()));
        await Interleaving.Within(initialized);
        await Interleaving.Settled(store);

        choreography.Failure.ShouldBeNull();
        foreach (var thread in choreography.Threads)
        {
            thread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }

        choreography.Entries.ShouldBe([
            "restore Hydrating",
            "restore Hydrating",
            "HydrationCompleted Hydrated",
            "StoreInitialized Hydrated",
            "Tick Hydrated",
            "Tick Hydrated",
        ]);
    }

    // §11.5 "Before StoreInitialized", §17.3 row 6: the attempt's read completes (its continuation claims, restores and
    // issues HydrationCompleted under _issue, inline on the completing thread) while an init-buffer overflow abort runs on
    // another thread (its init-token callback claims and issues HydrationFailed under _issue, then Abort reaches
    // MarkReady). Whatever the interleaving, exactly one terminal is processed, before StoreInitialized; a restore is
    // never processed after it, and the buffered actions are replayed after StoreInitialized. The deterministic twin is
    // HydrationTests.Hydration_ReadCompletesConcurrentlyWithInitAbort_TerminalPrecedesStoreInitialized_Deterministic.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Hydration_ReadCompletesConcurrentlyWithInitAbort_TerminalPrecedesStoreInitialized(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var read = Gen.Int.Operation<Race, Model>(_ => "CompleteRead", (a, _) => a.CompleteRead(), (_, _) => { });
        var abort = Gen.Int.Operation<Race, Model>(_ => "AbortInit", (a, _) => a.AbortInit(), (_, _) => { });

        // Bounded by progress (Interleaving.WithinProperty): every blocking step goes through Interleaving.Wait.
        await Interleaving.WithinProperty(Task.Run(
            () => Gen.Const(() => (new Race(), new Model())).SampleParallel(
                read,
                abort,
                equal: static (a, _) => a.TerminalPrecedesStoreInitialized,
                // Two distinct, idempotent operations: more parallel slots add threads, not interleavings, and none runs
                // sequentially first, so a sample with both is always a real race.
                maxSequentialOperations: 0,
                maxParallelOperations: 2),
            cancellationToken));
    }

    // One store per sample, never disposed (its timers are on a FakeTimeProvider that never moves): init started, its one
    // read pending, and two buffered user actions over InitBufferCapacity, so the overflow abort is queued, captured
    // rather than run. Each operation does its part once, whichever thread gets there first; the check does whatever
    // the run left undone, then waits for init and the last drain.
    private sealed class Race
    {
        private readonly TaskCompletionSource<object?> _read = new(); // inline continuations: the completing thread hydrates
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        private readonly Choreography _choreography = new();
        private readonly DuckyStore _store;
        private readonly Task _initialized;
        private Action? _abort;
        private bool? _verdict;

        public Race()
        {
            var provider = new ServiceCollection()
                .AddSingleton<TimeProvider>(_time)
                .AddSingleton<IJSRuntime>(new Js(_read.Task))
                .AddSingleton(_choreography)
                .AddDucky(d =>
                {
                    d.InitBufferCapacity = 1;
                    d.UseJson(ConcurrencyJson.Default).AddSlice<SavedSlice>()
                        .AddBlazor(static o => (o.IsBrowser, o.HydrationTimeout) = (true, _hydrationTimeout))
                        .Persist<SavedSlice>();
                    d.Use<Choreographer>();
                })
                .BuildServiceProvider();
            _store = (DuckyStore)provider.GetRequiredService<IStore>();
            List<Action> queued = [];
            _store.Dispatcher.QueueWorkItem = queued.Add;
            _initialized = _store.InitializeAsync(TestContext.Current.CancellationToken);
            _store.Dispatch(new Tick());
            _store.Dispatch(new Tick());
            _abort = queued.ShouldHaveSingleItem();
        }

        // Once, after the run (equal may be called once per linearization tried).
        public bool TerminalPrecedesStoreInitialized => _verdict ??= Check();

        public void CompleteRead() => _read.TrySetResult(EnvelopeWriter.Write("""{"Value":3}""", version: 1, _time.GetUtcNow()));

        public void AbortInit() => Interlocked.Exchange(ref _abort, null)?.Invoke();

        public override string ToString() => string.Join(", ", _choreography.Entries);

        private bool Check()
        {
            CompleteRead();
            AbortInit();
            Interleaving.Wait(_initialized);
            Interleaving.Wait(Interleaving.Settled(_store));

            var entries = _choreography.Entries;
            var terminals = entries.Select((entry, index) => (entry, index))
                .Where(static e => e.entry.StartsWith("HydrationCompleted ", StringComparison.Ordinal) || e.entry.StartsWith("HydrationFailed ", StringComparison.Ordinal))
                .Select(static e => e.index)
                .ToList();
            var initialized = entries.ToList().FindIndex(static e => e.StartsWith("StoreInitialized ", StringComparison.Ordinal));
            var lastRestore = entries.ToList().FindLastIndex(static e => e.StartsWith("restore ", StringComparison.Ordinal));
            return terminals.Count == 1
                && terminals[0] < initialized
                && lastRestore < terminals[0]
                && entries.Skip(initialized + 1).SequenceEqual(entries[terminals[0]].StartsWith("HydrationCompleted", StringComparison.Ordinal)
                    ? ["Tick Hydrated", "Tick Hydrated"]
                    : ["Tick Failed", "Tick Failed"]);
        }
    }

    private sealed class Model
    {
        public override string ToString() => "any interleaving";
    }

    private static Thread Start(Action work)
    {
        var thread = new Thread(() => work()) { IsBackground = true };
        thread.Start();
        return thread;
    }

    private static bool Blocked(Thread thread) => (thread.ThreadState & ThreadState.WaitSleepJoin) != 0;

    // Progress-bounded (Interleaving.Wait), spun on a dedicated thread: the winner's pool thread only waits for it.
    private static void Until(Func<bool> condition) => Interleaving.Wait(Task.Factory.StartNew(
        () =>
        {
            var spin = default(SpinWait);
            while (!condition())
            {
                spin.SpinOnce();
            }
        },
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default));

    private sealed class Choreography
    {
        private readonly Lock _gate = new();
        private readonly List<string> _entries = [];

        public Func<Thread[]>? Hold { get; set; }

        public Thread[] Threads { get; private set; } = [];

        public Exception? Failure { get; private set; }

        public IReadOnlyList<string> Entries
        {
            get
            {
                lock (_gate)
                {
                    return [.. _entries];
                }
            }
        }

        public void Add(string entry, bool restoresSaved)
        {
            lock (_gate)
            {
                _entries.Add(entry);
            }

            // The winner's restore: once, on the drain inside its restore under _issue.
            if (restoresSaved && Hold is { } hold)
            {
                Hold = null;
                try
                {
                    Threads = hold();
                }
#pragma warning disable CA1031 // justification: a failed step is asserted by the test, not thrown into the store's drain
                catch (Exception exception)
#pragma warning restore CA1031
                {
                    Failure = exception;
                }
            }
        }
    }

    // Registered after AddBlazor: sees the restores and the terminals after their reduce, on the draining thread.
    private sealed class Choreographer(Choreography choreography) : Middleware
    {
        public override void AfterReduce(ActionContext context)
        {
            var what = context.Origin == Origin.Hydration ? "restore" : context.Action.GetType().Name;
            var restoresSaved = context.Origin == Origin.Hydration && context.ChangedKeys.Contains(Store.Slices.OfType<SavedSlice>().Single().Key);
            choreography.Add($"{what} {context.State.Get<PersistenceState>().Status}", restoresSaved);
        }
    }

    // The JS runtime and the ducky.js module it imports: every storageGet answers with the one pending read.
    private sealed class Js(Task<object?> read) : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => identifier switch
        {
            "import" => new((TValue)(object)this),
            "storageGet" => new(As<TValue>(read)),
            _ => new(default(TValue)!),
        };

        public ValueTask DisposeAsync() => default;

        private static async Task<TValue> As<TValue>(Task<object?> value) => (TValue)(await value)!;
    }
}
