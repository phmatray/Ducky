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
