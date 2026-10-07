using Bunit;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Persistence;

// SPEC §11.5 (hydration at init: readable slices by gate, attempt 0 under _issue, one restore, exactly one terminal),
// §11.4 (the gate and its probe); INV-14. Storage is ducky.js behind a FakeJsRuntime (§17.2): storageGet answers from
// a dictionary keyed by area and storage key; a FakeJsStreamReference stored there is a value above InlinePayloadBytes,
// which storageGet answers with the too-large sentinel and storageGetStream pulls (§11.9).
public sealed class HydrationTests : BunitContext
{
    private const int InlinePayloadBytes = 16 * 1024;
    private static TimeSpan HydrationTimeout => TimeSpan.FromSeconds(5);
    private readonly FakeJsRuntime _js = new();
    private readonly Dictionary<(string Area, string Key), object?> _storage = [];
    private readonly List<FakeJsStreamReference> _pulled = [];
    private readonly HydrationLog _log = new();
    private readonly FakeLogCollector _logs;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
    private readonly CountingTimeProvider _clock;
    private Action<string>? _onLog;

    public HydrationTests()
    {
        _clock = new(_time);
        _js.Respond = (identifier, args) => identifier switch
        {
            "import" => _js,
            "storageGet" => _storage.GetValueOrDefault(((string)args[0]!, (string)args[1]!)) is FakeJsStreamReference
                ? BrowserStorageProvider.TooLarge
                : _storage.GetValueOrDefault(((string)args[0]!, (string)args[1]!)),
            "storageGetStream" => Pulled(_storage.GetValueOrDefault(((string)args[0]!, (string)args[1]!)) as FakeJsStreamReference
                ?? new FakeJsStreamReference([0])), // ducky.js's one NUL byte: nothing stored any more (S-5)
            _ => null,
        };

        // The sink runs synchronously as each record is logged, on the logging thread.
        _logs = new(Options.Create(new FakeLogCollectorOptions { OutputSink = line => _onLog?.Invoke(line) }));
    }

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private IStore Store => Services.GetRequiredService<IStore>();

    private PersistenceMiddleware Middleware => Services.GetRequiredService<PersistenceSlice>().Middleware.ShouldNotBeNull();

    private IEnumerable<object?[]> Reads => _js.Calls.Where(static call => call.Identifier == "storageGet").Select(static call => call.Args);

    [Fact]
    public async Task Hydration_Restored_OneTerminal_StatusOrder()
    {
        Browser(static d => d.Persist<CounterSlice>().Persist<TallySlice>(static o => o.Storage = PersistStorage.Session));
        _storage[("local", "ducky:counter")] = Envelope("""{ "Value" : 3 }""");
        _storage[("session", "ducky:tally")] = Envelope("""{"Value":7}""");

        await Store.InitializeAsync(Ct);

        // Hydrated (initial) → Hydrating → Hydrated: one restore of every read slice, then one terminal of epoch 0 issued
        // as a system action, all before StoreInitialized.
        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter,tally Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
        Store.State.Get<Counter>().ShouldBe(new Counter(3));
        Store.State.Get<Tally>().ShouldBe(new Tally(7));
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        Store.State.WasRestored<Tally>().ShouldBeTrue();

        // Unscoped keys {prefix}:{sliceKey}, each read once from its own area with the inline budget.
        Reads.ShouldBe([["local", "ducky:counter", InlinePayloadBytes], ["session", "ducky:tally", InlinePayloadBytes]]);

        // Attempt 0 (epoch 0) recorded every readable key; each read key's baseline is the local serialization of what was
        // read, never the stored text. No scope is in use, so none is recorded.
        Middleware.Current.ShouldNotBeNull().Epoch.ShouldBe(0);
        Middleware.Current.Keys.ShouldBe(["counter", "tally"], ignoreOrder: true);
        Middleware.LastKnownPayload.ShouldBe(
            new Dictionary<string, string> { ["ducky:counter"] = """{"Value":3}""", ["ducky:tally"] = """{"Value":7}""" }, ignoreOrder: true);
        Middleware.Scopes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Hydration_Empty_OneTerminal()
    {
        Browser(static d => d.Persist<CounterSlice>().Persist<TallySlice>().AddBlazor(static o => o.KeyPrefix = "app"));

        await Store.InitializeAsync(Ct);

        // Nothing stored: no slice restore, still exactly one terminal, which says nothing was restored.
        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "completed:False:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
        Store.State.WasRestored<Counter>().ShouldBeFalse();
        Store.State.Get<Counter>().ShouldBe(new Counter(0));
        Reads.Select(static args => args[1]).ShouldBe(["app:counter", "app:tally"]);
        Middleware.LastKnownPayload.ShouldBeEmpty();

        // The init-phase deadline, once the terminal is issued, does nothing.
        _time.Advance(HydrationTimeout);
        _log.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Hydration_NothingPersisted_Inert()
    {
        Browser(static _ => { });

        await Store.InitializeAsync(Ct);

        // No Persist<T>: no attempt, no terminal, no interop at all, and Status stays the initial Hydrated.
        _log.Entries.ShouldBe(["initialized Hydrated:0"]);
        Store.State.Get<PersistenceState>().ShouldBeSameAs(Store.InitialState.Get<PersistenceState>());
        Store.State.WasRestored<PersistenceState>().ShouldBeFalse();
        _js.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Hydration_ServerStorageOnly_NotReadThroughBrowserStorage()
    {
        // (non-normative) Server storage never goes through ducky.js: a store whose persisted slices are all Server storage
        // has nothing to read here, so it stays Hydrated with no interop (the cache provider comes with §11.6).
        Browser(static d => d.Persist<CounterSlice>(static o => o.Storage = PersistStorage.Server));

        await Store.InitializeAsync(Ct);

        _log.Entries.ShouldBe(["initialized Hydrated:0"]);
        _js.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Hydration_NonInteractiveStore_NotStarted()
    {
        // (non-normative) Prerender or static SSR with browser-only slices: nothing is readable, so Status goes to
        // NotStarted, with no attempt and so no terminal. A store touched first by non-component code probes the gate.
        _js.Respond = static (_, _) => throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");
        Server(static d => d.Persist<CounterSlice>());

        await Store.InitializeAsync(Ct);

        _log.Entries.ShouldBe(["restore:@ducky/persistence NotStarted:0", "initialized NotStarted:0"]);
        _js.Calls.Select(static call => call.Identifier).ShouldBe(["import"]); // the probe, never a read
    }

    [Fact]
    public async Task Hydration_PrerenderComponentFirst_NotStarted()
    {
        // (non-normative) A prerendering component recorded its renderer first: non-interactive without any probe.
        Server(static d => d.Persist<CounterSlice>());
        Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        Render<CounterView>().Markup.ShouldBe("0");
        await Store.InitializeAsync(Ct);

        Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.NotStarted);
        _js.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("nothing persisted")]
    [InlineData("empty")]
    [InlineData("restored")]
    [InlineData("unreadable")]
    [InlineData("interrupted")]
    [InlineData("throws")]
    public async Task Hydration_TerminalActions_Paired(string storage)
    {
        Browser(d =>
        {
            if (storage != "nothing persisted")
            {
                d.Persist<CounterSlice>().Persist<TallySlice>(static o => o.Storage = PersistStorage.Session);
            }
        });
        _storage[("local", "ducky:counter")] = storage switch
        {
            "restored" => Envelope("""{"Value":2}"""),
            "unreadable" => "not json",
            "interrupted" => Task.FromException<object?>(new JSDisconnectedException("circuit gone")), // never read: not "not found"
            "throws" => Task.FromException<object?>(new JSException("SecurityError")), // storage disabled
            _ => null,
        };

        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        // Every move to Hydrating opens an attempt of its epoch, which exactly one terminal of the same epoch closes
        // before StoreInitialized; no terminal without an attempt.
        List<string> entries = [.. _log.Entries];
        var opened = entries.Where(static e => e.StartsWith("restore:@ducky/persistence Hydrating:", StringComparison.Ordinal)).ToList();
        var terminals = entries.Where(static e => e.StartsWith("completed:", StringComparison.Ordinal) || e.StartsWith("failed:", StringComparison.Ordinal)).ToList();
        terminals.Count.ShouldBe(opened.Count);
        terminals.Count.ShouldBe(storage == "nothing persisted" ? 0 : 1);
        for (var i = 0; i < opened.Count; i++)
        {
            var epoch = opened[i].Split(':')[^1];
            terminals[i].Split(' ')[0].Split(':')[2].ShouldBe(epoch);
            entries.IndexOf(terminals[i]).ShouldBeGreaterThan(entries.IndexOf(opened[i]));
            entries.IndexOf(terminals[i]).ShouldBeLessThan(entries.FindIndex(static e => e.StartsWith("initialized ", StringComparison.Ordinal)));
        }

        // A read that failed or never happened fails the attempt; it never reports a successful (empty) hydration.
        var failed = storage is "interrupted" or "throws";
        terminals.ShouldAllBe(t => t.StartsWith(failed ? "failed:" : "completed:", StringComparison.Ordinal));
        Store.State.Get<PersistenceState>().Status.ShouldBe(failed ? PersistenceStatus.Failed : PersistenceStatus.Hydrated);
        Store.State.WasRestored<Counter>().ShouldBe(storage == "restored");
    }

    [Theory]
    [InlineData("todos", """{"Items":[{"Id":1,"Title":"a"},{"Id":1,"Title":"b"}]}""", typeof(EntityState<int, Todo>))]
    [InlineData("picky", """{"Value":-1}""", typeof(Picky))]
    public async Task Hydration_OneSliceCtorThrows_OtherSlicesStillRestored(string key, string payload, Type stateType)
    {
        Browser(static d => d.AddSlice<TodosSlice>().AddSlice<PickySlice>()
            .Persist<CounterSlice>().Persist<TodosSlice>().Persist<PickySlice>().Persist<TallySlice>());
        _storage[("local", "ducky:counter")] = Envelope("""{"Value":3}""");
        _storage[("local", $"ducky:{key}")] = Envelope(payload);
        _storage[("local", "ducky:tally")] = Envelope("""{"Value":7}""");

        await Store.InitializeAsync(Ct);

        // The throwing slice keeps its initial state, never fails the attempt, and the others restore in the one restore.
        Store.State.Get<Counter>().ShouldBe(new Counter(3));
        Store.State.Get<Tally>().ShouldBe(new Tally(7));
        Store.State.WasRestored(key).ShouldBeFalse();
        Store.State.Get(key).ShouldBeSameAs(Store.InitialState.Get(key));
        _log.Entries.ShouldContain("completed:True:0:System Hydrated:0");

        // One Warning naming the storage key and the declared type.
        var warning = _logs.GetSnapshot().Where(static r => r.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldContain($"ducky:{key}");
        warning.Message.ShouldContain(stateType.Name);
    }

    [Fact]
    public async Task Hydration_ServerInteractive_BrowserReadBeforeStoreInitialized()
    {
        // Interactive Server: the circuit store reads localStorage across the hub, so the read completes later. It still
        // completes, restores and issues its terminal before StoreInitialized.
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage[("local", "ducky:counter")] = read.Task;
        Server(static d => d.Persist<CounterSlice>());
        Renderer.SetRendererInfo(new RendererInfo("Server", isInteractive: true));

        // The component hands over (interactive) and starts init: Hydrating, the read in flight, StoreInitialized waiting.
        var cut = Render<CounterView>();
        var initialized = Store.InitializeAsync(Ct);
        cut.Markup.ShouldBe("0");
        initialized.IsCompleted.ShouldBeFalse();
        _log.Entries.ShouldBe(["restore:@ducky/persistence Hydrating:0"]);
        Reads.ShouldBe([["local", "ducky:counter", InlinePayloadBytes]]);

        read.SetResult(Envelope("""{"Value":5}"""));
        await initialized;

        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
        cut.WaitForAssertion(() => cut.Markup.ShouldBe("5"));
    }

    [Fact]
    public async Task Hydration_LoadThrows_FailedGateReleasedPersistenceResumes()
    {
        // §11.5 step 7, §7 (failure table): a read that throws (storage disabled) ends the attempt in HydrationFailed, a
        // failure action of epoch 0, before StoreInitialized. Init is released at once (the action buffered during it is
        // replayed after StoreInitialized, never held for HydrationTimeout), and the store goes on: Status is Failed, not
        // Hydrating, so nothing is held back as "still hydrating" any more, and the init-phase deadline does nothing later.
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage[("local", "ducky:counter")] = read.Task;
        Browser(static d => d.Persist<CounterSlice>());
        int? baselineAtTerminal = null; // read inside the drain, before the buffered Increment is replayed
        _log.OnEntry = entry =>
        {
            if (entry.StartsWith("failed:", StringComparison.Ordinal))
            {
                baselineAtTerminal = Middleware.LastKnownPayload.Count;
            }
        };

        var initialized = Store.InitializeAsync(Ct);
        Store.Dispatch(new Increment());
        read.SetException(new JSException("SecurityError: the operation is insecure"));
        await initialized;
        await Store.WhenIdleAsync(Ct);

        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "failed:JSException:0:System Failed:0",
            "initialized Failed:0",
            "Ducky.Blazor.Tests.Core.Increment Failed:0",
        ]);
        Store.State.WasRestored<Counter>().ShouldBeFalse();
        Middleware.Current.ShouldNotBeNull().Terminal.ShouldBe(2);
        baselineAtTerminal.ShouldBe(0); // the failed read set no baseline

        // The terminal disarmed the init-phase timer (§11.5 step 7): reaching HydrationTimeout fires nothing.
        await Store.DispatchAsync(new Increment());
        _time.Advance(HydrationTimeout);
        _clock.Fired.ShouldBe(0);
        Store.State.Get<Counter>().ShouldBe(new Counter(2));
        _log.Entries.Count.ShouldBe(5);

        // Persistence resumes: the writer's hydration skip (§11.5 Writes, ShouldSkip case (a)) holds only while Status is
        // Hydrating, and a change to Failed is what drains _deferred, so no key is held back after the failure. The writer
        // ends on Counter(2), written once (the replayed Increment's Counter(1) may or may not be written before it).
        Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Failed);
        await WriterHarness.Until(() => Middleware.LastKnownPayload.GetValueOrDefault("ducky:counter") == """{"Value":2}""");
        var written = _js.Calls
            .Where(static call => call.Identifier == "storageSet" && (string)call.Args[1]! == "ducky:counter")
            .Select(static call => new StoredWrite((string)call.Args[0]!, (string)call.Args[1]!, (string)call.Args[2]!, (string)call.Args[3]!).Payload)
            .ToList();
        written.Count(static payload => payload == """{"Value":2}""").ShouldBe(1);
        written[^1].ShouldBe("""{"Value":2}""");
    }

    [Theory]
    [InlineData("load throws")]
    [InlineData("timeout")]
    [InlineData("completed")]
    public async Task Hydration_Terminal_IssuedAsFailureOnlyWhenFailed(string outcome)
    {
        // §11.5 step 7, INV-12: HydrationFailed is dispatched with isFailure, so a reducer that throws on it is logged only
        // (1001) and never routed as a ReducerFailed; HydrationCompleted is dispatched normally, so the same throw is routed.
        // A throwing reducer discards the action's whole reduce, which is why the named failure tests above go without it.
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage[("local", "ducky:counter")] = outcome == "completed" ? Envelope("""{"Value":3}""") : read.Task;
        Browser(static d => d.Persist<CounterSlice>().AddSlice<CrashOnHydrationTerminalSlice>().AddSlice<FailureSlice>());

        var initialized = Store.InitializeAsync(Ct);
        if (outcome == "load throws")
        {
            read.SetException(new JSException("SecurityError: the operation is insecure"));
        }
        else if (outcome == "timeout")
        {
            _time.Advance(HydrationTimeout);
        }

        await initialized;
        await Store.WhenIdleAsync(Ct);

        var routed = Store.State.Get<Failures>().Items;
        var loggedOnly = _logs.GetSnapshot().Where(static r => r.Id.Id == 1001).ToList();
        if (outcome == "completed")
        {
            routed.ShouldHaveSingleItem().ActionType.ShouldBe(typeof(HydrationCompleted).FullName);
            loggedOnly.ShouldBeEmpty();
        }
        else
        {
            routed.ShouldBeEmpty();
            loggedOnly.ShouldHaveSingleItem().Message.ShouldContain(nameof(HydrationFailed));
        }
    }

    [Theory]
    [InlineData("read first")]
    [InlineData("abort first")]
    public async Task Hydration_ReadCompletesConcurrentlyWithInitAbort_TerminalPrecedesStoreInitialized_Deterministic(string winner)
    {
        // The TCS-gated twin of the Ducky.Concurrency.Tests race (§11.5 "Before StoreInitialized", §6.7): an init-buffer
        // overflow aborts init (the abort runs on a pool thread) while the attempt's read result is in hand.
        // - read first: the winner claims, and its restore drains inline while it holds _issue. There the second Increment
        //   overflows the init buffer, and the winner blocks until the abort's init-token callbacks have started (the
        //   recorder's completes AbortThread just before the persistence one runs), then until that thread is waiting, on
        //   _issue. So the abort has won the init CAS (Complete waits for the init task, which the winner's terminal
        //   completes) and its callback gets _issue only after the winner's terminal is queued: had it not waited for
        //   _issue, Abort would reach MarkReady first and StoreInitialized would precede HydrationCompleted.
        // - abort first: the read has completed, but the attempt still waits for the prerender seed to settle (no component
        //   registers, so only the abort settles it). The init-token callback claims and issues HydrationFailed; the result
        //   then loses the claim and is discarded with a Debug log, never restored after StoreInitialized.
        var read = new TaskCompletionSource<object?>();
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage[("local", "ducky:counter")] = winner == "read first" ? read.Task : Envelope("""{"Value":3}""");
        var abortEnded = false; // the abort reached MarkReady: logged after its init-token callbacks returned
        _onLog = line =>
        {
            if (line.Contains("read result of hydration attempt", StringComparison.Ordinal))
            {
                discarded.TrySetResult();
            }

            if (line.Contains("Store init aborted", StringComparison.Ordinal))
            {
                Volatile.Write(ref abortEnded, true);
            }
        };
        Browser(d =>
        {
            d.InitBufferCapacity = 1;
            d.Persist<CounterSlice>();
            if (winner == "abort first")
            {
                d.Prerender<TallySlice>();
            }
        });
        var blockedOnIssue = false;
        _log.OnEntry = entry =>
        {
            if (entry.StartsWith("restore:counter", StringComparison.Ordinal))
            {
                Store.Dispatch(new Increment());
                Store.Dispatch(new Increment());

                // A blocking wait, so a cold pool sees this thread blocked and runs the queued abort. The bound only keeps
                // a regression from hanging the run; asserted outside the drain, which isolates a throw.
                if (!_log.AbortThread.Task.Wait(TimeSpan.FromSeconds(30), Ct))
                {
                    return;
                }

                // The abort thread is running its callbacks; its next wait is EndInitPhase's lock (_issue), which this
                // thread holds, unless it skips _issue and ends (abortEnded). Either comes within a few instructions, so
                // this spin needs no bound.
                var abort = _log.AbortThread.Task.Result;
                var spin = default(SpinWait);
                while (!Volatile.Read(ref abortEnded) && (abort.ThreadState & System.Threading.ThreadState.WaitSleepJoin) == 0)
                {
                    spin.SpinOnce(sleep1Threshold: -1);
                }

                blockedOnIssue = !Volatile.Read(ref abortEnded);
            }
        };

        var initialized = Store.InitializeAsync(Ct);
        if (winner == "read first")
        {
            read.SetResult(Envelope("""{"Value":3}"""));
        }
        else
        {
            Store.Dispatch(new Increment());
            Store.Dispatch(new Increment());
        }

        await initialized;
        await Store.WhenIdleAsync(Ct);

        if (winner == "read first")
        {
            blockedOnIssue.ShouldBeTrue("the abort ended without waiting for _issue, which the winner held");
            _log.Entries.ShouldBe([
                "restore:@ducky/persistence Hydrating:0",
                "restore:counter Hydrating:0",
                "completed:True:0:System Hydrated:0",
                "initialized Hydrated:0",
                "Ducky.Blazor.Tests.Core.Increment Hydrated:0",
                "Ducky.Blazor.Tests.Core.Increment Hydrated:0",
            ]);
            Store.State.Get<Counter>().ShouldBe(new Counter(5));
            discarded.Task.IsCompleted.ShouldBeFalse();
            _log.InitToken.IsCancellationRequested.ShouldBeTrue();
        }
        else
        {
            // The fake clock never moves here: the wall-clock bound only keeps a regression from hanging the run.
            await discarded.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            _log.Entries.ShouldBe([
                "restore:@ducky/persistence Hydrating:0",
                "failed:OperationCanceledException:0:System Failed:0",
                "initialized Failed:0",
                "Ducky.Blazor.Tests.Core.Increment Failed:0",
                "Ducky.Blazor.Tests.Core.Increment Failed:0",
            ]);
            Store.State.Get<Counter>().ShouldBe(new Counter(2));
            Store.State.WasRestored<Counter>().ShouldBeFalse(); // with the 2041 log above: the result was discarded
        }
    }

    [Fact]
    public async Task Hydration_Timeout_Failed()
    {
        // §11.5 step 7: the init-phase deadline armed in the synchronous prefix ends attempt 0 with
        // HydrationFailed, before StoreInitialized and long before InitTimeout. The cancelled read never restores.
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage[("local", "ducky:counter")] = read.Task;
        Server(static d => d.Persist<CounterSlice>());

        var initialized = Store.InitializeAsync(Ct);
        _time.Advance(HydrationTimeout - TimeSpan.FromTicks(1));
        _log.Entries.ShouldBe(["restore:@ducky/persistence Hydrating:0"]);
        _time.Advance(TimeSpan.FromTicks(1));
        await initialized;
        read.SetResult(Envelope("""{"Value":5}"""));

        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "failed:TimeoutException:0:System Failed:0",
            "initialized Failed:0",
        ]);
        Store.State.WasRestored<Counter>().ShouldBeFalse();
    }

    [Fact]
    public async Task Hydration_InitAborted_OneTerminal()
    {
        // §11.5 step 7, §6.7: an init abort (here an init-buffer overflow) cancels the init token while the
        // read is pending; the registration made in the synchronous prefix issues HydrationFailed before StoreInitialized,
        // and the interrupted read never counts as "nothing stored".
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage[("local", "ducky:counter")] = read.Task;
        Server(static d =>
        {
            d.InitBufferCapacity = 1;
            d.Persist<CounterSlice>();
        });

        var initialized = Store.InitializeAsync(Ct);
        Store.Dispatch(new Increment());
        Store.Dispatch(new Increment());
        await initialized;
        await Store.WhenIdleAsync(Ct);

        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "failed:OperationCanceledException:0:System Failed:0",
            "initialized Failed:0",
            "Ducky.Blazor.Tests.Core.Increment Failed:0",
            "Ducky.Blazor.Tests.Core.Increment Failed:0",
        ]);
        Store.State.WasRestored<Counter>().ShouldBeFalse();
    }

    [Fact]
    public async Task Hydration_ResultAfterTimeout_IsDiscarded()
    {
        // §11.5 step 6: the deadline fires after the reads ended but before the result reached _issue (here, from inside
        // the last read, as its Warning is logged); the deadline's HydrationFailed is the one terminal and the late result
        // is discarded, with a Debug log: no restore, no baseline.
        Browser(static d => d.Persist<CounterSlice>().Persist<TallySlice>());
        _storage[("local", "ducky:counter")] = Envelope("""{"Value":3}""");
        _storage[("local", "ducky:tally")] = "not json";
        _onLog = line =>
        {
            if (line.Contains("ducky:tally", StringComparison.Ordinal))
            {
                _time.Advance(HydrationTimeout);
            }
        };

        await Store.InitializeAsync(Ct);

        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "failed:TimeoutException:0:System Failed:0",
            "initialized Failed:0",
        ]);
        Store.State.WasRestored<Counter>().ShouldBeFalse();
        Middleware.LastKnownPayload.ShouldBeEmpty();
        var discarded = _logs.GetSnapshot().Where(static r => r.Id.Id == 2041).ShouldHaveSingleItem();
        discarded.Level.ShouldBe(LogLevel.Debug);
    }

    [Fact]
    public async Task Hydration_DeadlineDuringWinnersRestore_ItsTerminalIssuedOnce()
    {
        // (non-normative) §11.5 "Exactly one terminal": the deadline fires re-entrantly on the winner's thread, inside its
        // restore's drain, after the winner claimed and before it issued. The deadline loses the claim yet issues the
        // winner's claimed terminal (Terminal is 2 as soon as it returns, so an init abort ending the same way finds it
        // queued before MarkReady), and the winner's own IssueTerminal then does nothing: one terminal in the log.
        Browser(static d => d.Persist<CounterSlice>());
        _storage[("local", "ducky:counter")] = Envelope("""{"Value":3}""");
        int? terminalAfterDeadline = null;
        _log.OnEntry = entry =>
        {
            if (entry.StartsWith("restore:counter", StringComparison.Ordinal))
            {
                _time.Advance(HydrationTimeout);
                terminalAfterDeadline = Middleware.Current!.Terminal;
            }
        };

        await Store.InitializeAsync(Ct);

        terminalAfterDeadline.ShouldBe(2);
        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
    }

    [Fact]
    public async Task LargeRead_UsesStreamAboveInlineLimit()
    {
        // §11.5 step 4, D11: above InlinePayloadBytes storageGet answers the sentinel and the value is pulled through
        // storageGetStream (strings only cross, INV-23), decoded as UTF-8; a key removed between the two calls pulls one
        // NUL byte (Length <= 1, S-5) and reads as not found. Every reference is disposed after it is read.
        Browser(static d => d.AddSlice<TodosSlice>()
            .Persist<CounterSlice>().Persist<TodosSlice>().Persist<TallySlice>(static o => o.Storage = PersistStorage.Session));
        var title = new string('é', InlinePayloadBytes);
        _storage[("local", "ducky:counter")] = Envelope("""{"Value":3}""");
        _storage[("local", "ducky:todos")] = Stream(Envelope($$"""{"Items":[{"Id":1,"Title":"{{title}}"}]}"""));
        _storage[("session", "ducky:tally")] = BrowserStorageProvider.TooLarge; // the sentinel, then gone before the pull

        await Store.InitializeAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(3));
        Store.State.Get<EntityState<int, Todo>>().Items.ShouldHaveSingleItem().Title.ShouldBe(title);
        Store.State.WasRestored<Tally>().ShouldBeFalse();
        _log.Entries.ShouldContain("completed:True:0:System Hydrated:0");
        _js.Calls.Where(static c => c.Identifier == "storageGetStream").Select(static c => c.Args)
            .ShouldBe([["session", "ducky:tally"], ["local", "ducky:todos"]]); // in slice registration order
        _pulled.Count.ShouldBe(2);
        _pulled.ShouldAllBe(static r => r.Disposed);
        _pulled[0].Opened.ShouldBeFalse(); // the one-byte reference of the removed key is never read
        _logs.GetSnapshot().ShouldNotContain(static r => r.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task LargeRead_MaxPayloadBytesBoundary(int over)
    {
        // §11.5 step 4: a value at MaxPayloadBytes restores (maxAllowedSize is MaxPayloadBytes, the fake faults above it);
        // one byte above reads as not found with Warning 2023 naming the key and its size, decided from Length before the
        // stream is opened, and its reference is disposed all the same. The other keys restore.
        const int MaxPayloadBytes = 64 * 1024;
        Browser(static d => d.AddBlazor(static o => o.MaxPayloadBytes = MaxPayloadBytes).Persist<CounterSlice>().Persist<TallySlice>());
        _storage[("local", "ducky:counter")] = Envelope("""{"Value":3}""");
        var envelope = Envelope("""{"Value":7}""");
        _storage[("local", "ducky:tally")] = Stream(envelope.PadRight(MaxPayloadBytes + over));

        await Store.InitializeAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(3));
        Store.State.WasRestored<Tally>().ShouldBe(over == 0);
        Store.State.Get<Tally>().ShouldBe(over == 0 ? new Tally(7) : Store.InitialState.Get<Tally>());
        _log.Entries.ShouldContain("completed:True:0:System Hydrated:0");
        _pulled.ShouldHaveSingleItem().Disposed.ShouldBeTrue();
        _pulled[0].Opened.ShouldBe(over == 0);
        var warnings = _logs.GetSnapshot().Where(static r => r.Level >= LogLevel.Warning).ToList();
        if (over == 0)
        {
            warnings.ShouldBeEmpty();
            return;
        }

        var warning = warnings.ShouldHaveSingleItem();
        warning.Id.Id.ShouldBe(2023);
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain("ducky:tally");
        warning.Message.ShouldContain((MaxPayloadBytes + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task LargeRead_PullFails_KeyNotFound_OtherSlicesRestored()
    {
        // §11.5 step 4, "a failed pull is caught per key and never fails the attempt": storageGetStream rejecting (the
        // runtime refusing the export's return, S-5) reads that key as not found with a Warning naming it; the others restore.
        Browser(static d => d.Persist<CounterSlice>().Persist<TallySlice>(static o => o.Storage = PersistStorage.Session));
        _storage[("local", "ducky:counter")] = Stream(Envelope("""{"Value":3}"""));
        _storage[("session", "ducky:tally")] = Envelope("""{"Value":7}""");
        var respond = _js.Respond;
        var failure = new JSException("Length must be a positive value");
        _js.Respond = (identifier, args) => identifier == "storageGetStream" ? Task.FromException<object?>(failure) : respond(identifier, args);

        await Store.InitializeAsync(Ct);

        Store.State.WasRestored<Counter>().ShouldBeFalse();
        Store.State.Get<Tally>().ShouldBe(new Tally(7));
        _log.Entries.ShouldContain("completed:True:0:System Hydrated:0");
        var warning = _logs.GetSnapshot().Where(static r => r.Level >= LogLevel.Warning).ShouldHaveSingleItem();
        (warning.Id.Id, warning.Level).ShouldBe((2026, LogLevel.Warning));
        warning.Message.ShouldContain("'ducky:counter'");
        warning.Exception.ShouldBeSameAs(failure);
    }

    [Theory]
    [InlineData("value")]
    [InlineData("gone")]
    [InlineData("over")]
    public async Task LargeRead_DisposeDisconnected_ReadStands(string pulled)
    {
        // §11.9: a .NET-side disposal of an IJSStreamReference catches JSDisconnectedException. A circuit dropped after the
        // pull delivered never turns a value read in full, or a key read as not found, into a failed attempt.
        const int MaxPayloadBytes = 64 * 1024;
        Browser(static d => d.AddBlazor(static o => o.MaxPayloadBytes = MaxPayloadBytes).Persist<CounterSlice>());
        _storage[("local", "ducky:counter")] = BrowserStorageProvider.TooLarge;
        var content = pulled switch
        {
            "value" => Envelope("""{"Value":3}"""),
            "gone" => "\0",
            _ => new string(' ', MaxPayloadBytes + 1),
        };
        var respond = _js.Respond;
        _js.Respond = (identifier, args) => identifier == "storageGetStream"
            ? Pulled(new(System.Text.Encoding.UTF8.GetBytes(content)) { OnDispose = new JSDisconnectedException("circuit gone") })
            : respond(identifier, args);

        await Store.InitializeAsync(Ct);

        _log.Entries.ShouldContain($"completed:{pulled == "value"}:0:System Hydrated:0"); // restoredAny: only the value restores
        Store.State.WasRestored<Counter>().ShouldBe(pulled == "value");
        _pulled.ShouldHaveSingleItem().Disposed.ShouldBeTrue();
        _logs.GetSnapshot().ShouldContain(static r => r.Id.Id == 2000 && r.Message.Contains("'dispose'") && r.Exception is JSDisconnectedException);
    }

    [Fact]
    public async Task LargeRead_PullInterrupted_AttemptFailed()
    {
        // (non-normative) How "a failed pull is caught per key" (§11.5 step 4) is read here: a pull that FAILED (the runtime
        // rejected it: LargeRead_PullFails_KeyNotFound_OtherSlicesRestored) is not found for that key, but a pull that never
        // happened (a disconnect, an interop timeout or the token) is an interrupted read, as for storageGet, and fails the
        // attempt (step 7): read as not found, the slice's load would run and its next write replace the stored value.
        Services.AddSingleton<HydrationFailures>();
        Browser(static d => d.Persist<CounterSlice>().Use<HydrationFailureRecorder>());
        _storage[("local", "ducky:counter")] = Stream(Envelope("""{"Value":3}"""));
        var respond = _js.Respond;
        _js.Respond = (identifier, args) => identifier == "storageGetStream"
            ? Task.FromException<object?>(new JSDisconnectedException("circuit gone"))
            : respond(identifier, args);

        await Store.InitializeAsync(Ct);

        _log.Entries.ShouldContain("failed:InvalidOperationException:0:System Failed:0");
        Services.GetRequiredService<HydrationFailures>().ShouldHaveSingleItem().ShouldContain("'ducky:counter' from local storage");
        Store.State.WasRestored<Counter>().ShouldBeFalse();
    }

    [Fact]
    public async Task Hydration_OneOversizedKey_OtherSlicesRestored()
    {
        // INV-14: one key above MaxPayloadBytes (the default, 5 MiB) is not found for that key only; it never fails the
        // attempt, and the other slices restore in the one restore.
        Browser(static d => d.Persist<CounterSlice>().Persist<TallySlice>(static o => o.Storage = PersistStorage.Session));
        _storage[("local", "ducky:counter")] = Stream(Envelope("""{"Value":3}""").PadRight((5 * 1024 * 1024) + 1));
        _storage[("session", "ducky:tally")] = Envelope("""{"Value":7}""");

        await Store.InitializeAsync(Ct);

        Store.State.WasRestored<Counter>().ShouldBeFalse();
        Store.State.Get<Tally>().ShouldBe(new Tally(7));
        _log.Entries.ShouldContain("completed:True:0:System Hydrated:0");
        Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Hydrated);
        _logs.GetSnapshot().Where(static r => r.Level >= LogLevel.Warning).ShouldHaveSingleItem().Id.Id.ShouldBe(2023);
    }

    [Fact]
    public async Task Hydration_RestoredStateNotSerializable_RestoredWithoutBaseline()
    {
        // (non-normative) A state that reads but can't be serialized back is restored with no baseline (the writer reports it).
        Browser(static d => d.AddSlice<UnwritableSlice>().Persist<UnwritableSlice>().Persist<CounterSlice>());
        _storage[("local", "ducky:unwritable")] = Envelope("""{"Value":1}""");
        _storage[("local", "ducky:counter")] = Envelope("""{"Value":3}""");

        await Store.InitializeAsync(Ct);

        Store.State.WasRestored("unwritable").ShouldBeTrue();
        Middleware.LastKnownPayload.Keys.ShouldBe(["ducky:counter"]);
    }

    [Fact]
    public async Task Hydration_NoJsRuntime_ServerStoreNotStarted()
    {
        // (non-normative) A host without an IJSRuntime (a console or a plain DI container) can't read browser storage: the
        // probe reads it as non-interactive.
        Server(static d => d.Persist<CounterSlice>(), js: false);

        await Store.InitializeAsync(Ct);

        _log.Entries.ShouldBe(["restore:@ducky/persistence NotStarted:0", "initialized NotStarted:0"]);
    }

    [Fact]
    public async Task Hydration_NoJsRuntimeInBrowser_Failed()
    {
        // (non-normative) The same in a browser store: the read fails, so the attempt ends in HydrationFailed, never hangs.
        Browser(static d => d.Persist<CounterSlice>(), js: false);

        await Store.InitializeAsync(Ct);

        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "failed:InvalidOperationException:0:System Failed:0",
            "initialized Failed:0",
        ]);
    }

    [Fact]
    public async Task Hydration_Scope_ReadsOnlyTheScopedKey()
    {
        // (non-normative) §11.6: with a Scope, Local/Session keys are {prefix}:{scope}:{sliceKey}. The scope is resolved for
        // epoch 0 and recorded in the epoch-to-scope map; the shared unscoped key is never read.
        Server(static d => d.Persist<CounterSlice>().AddBlazor(static o => o.Scope = static (_, _) => ValueTask.FromResult<string?>("alice")));
        _storage[("local", "ducky:counter")] = Envelope("""{"Value":1}""");
        _storage[("local", "ducky:alice:counter")] = Envelope("""{"Value":4}""");

        await Store.InitializeAsync(Ct);

        Store.State.Get<Counter>().ShouldBe(new Counter(4));
        Reads.Select(static args => args[1]).ShouldBe(["ducky:alice:counter"]);
        Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = "alice" });
        Middleware.LastKnownPayload.Keys.ShouldBe(["ducky:alice:counter"]);
    }

    [Fact]
    public async Task Hydration_NullScope_NoReadNoFallback()
    {
        // (non-normative) §11.6: a scope that resolves null means no I/O for the scoped keys, never the shared key; the
        // attempt still ends in one terminal, and the null scope is recorded for epoch 0.
        Server(static d => d.Persist<CounterSlice>().AddBlazor(static o => o.Scope = static (_, _) => ValueTask.FromResult<string?>(null)));
        _storage[("local", "ducky:counter")] = Envelope("""{"Value":1}""");

        await Store.InitializeAsync(Ct);

        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "completed:False:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
        Reads.ShouldBeEmpty();
        Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = null });
    }

    [Fact]
    public async Task Hydration_BrowserScope_WaitsForTheRendererHandOff()
    {
        // (non-normative) §11.6: in the browser the scope delegate gets the renderer's services, so scoped hydration waits for
        // the first component's hand-off, then reads.
        IServiceProvider? seen = null;
        Browser(d => d.Persist<CounterSlice>().AddBlazor(o => o.Scope = (services, _) =>
        {
            seen = services;
            return ValueTask.FromResult<string?>("alice");
        }));
        _storage[("local", "ducky:alice:counter")] = Envelope("""{"Value":4}""");
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));

        var initialized = Store.InitializeAsync(Ct);
        initialized.IsCompleted.ShouldBeFalse();
        Reads.ShouldBeEmpty();

        var cut = Render<CounterView>();
        await initialized;

        seen.ShouldNotBeNull().ShouldBeSameAs(Services.GetRequiredService<PersistenceSlice>().Gate.Services);
        Store.State.Get<Counter>().ShouldBe(new Counter(4));
        cut.WaitForAssertion(() => cut.Markup.ShouldBe("4"));
    }

    [Fact]
    public async Task Hydration_ScopeIgnoresToken_InitEndsAtHydrationTimeout_LateScopeRecorded()
    {
        // (non-normative) §11.5 "Before StoreInitialized", §11.6: a Scope delegate that ignores its token (no token overload
        // to pass it to) is bounded by the same HydrationTimeout: InitializeAsync returns with the init-phase terminal, not
        // with the delegate, so StoreInitialized comes long before InitTimeout and init is never aborted. The late scope is
        // still recorded for epoch 0, for the writers.
        // Inline continuations: the late recording has run by the time SetResult returns.
        var scope = new TaskCompletionSource<string?>();
        Server(d => d.Persist<CounterSlice>().AddBlazor(o => o.Scope = (_, _) => new(scope.Task)));

        var initialized = Store.InitializeAsync(Ct);
        _time.Advance(HydrationTimeout);
        // The fake clock never reaches InitTimeout: completing at all is completing before it. The wall-clock bound only
        // keeps a regression from hanging the run; the delegate is released either way, or the store's dispose would wait.
        var ended = initialized.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        await ended.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        var entries = _log.Entries;
        var scopesBefore = Middleware.Scopes;
        scope.SetResult("alice");

        ended.IsCompletedSuccessfully.ShouldBeTrue("InitializeAsync waited for the Scope delegate past HydrationTimeout");
        entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "failed:TimeoutException:0:System Failed:0",
            "initialized Failed:0",
        ]);
        _logs.GetSnapshot().ShouldNotContain(static r => r.Message.StartsWith("Store init aborted", StringComparison.Ordinal));
        scopesBefore.ShouldBeEmpty();
        Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = "alice" });
        Reads.ShouldBeEmpty();
    }

    private string Envelope(string payload) => EnvelopeWriter.Write(payload, version: 1, _time.GetUtcNow());

    private static FakeJsStreamReference Stream(string value) => new(System.Text.Encoding.UTF8.GetBytes(value));

    private FakeJsStreamReference Pulled(FakeJsStreamReference reference)
    {
        _pulled.Add(reference);
        return reference;
    }

    // A WASM store: one per app, interactive by construction.
    private void Browser(Action<DuckyBuilder> ducky, bool js = true) => Configure(d => ducky(d.AddBlazor(static o => o.IsBrowser = true)), js);

    // A server store (prerender or circuit): interactive or not as its gate decides.
    private void Server(Action<DuckyBuilder> ducky, bool js = true) => Configure(ducky, js);

    private void Configure(Action<DuckyBuilder> ducky, bool js = true)
    {
        // Without js, no IJSRuntime at all (bUnit registers its own).
        Services.RemoveAll<IJSRuntime>();
        if (js)
        {
            Services.AddSingleton<IJSRuntime>(_js);
        }

        Services.AddSingleton<TimeProvider>(_clock);
        Services.AddSingleton(_log);
        Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs)));
        Services.AddDucky(d =>
        {
            d.UseJson(HydrationJson.Default).AddSlice<CounterSlice>().AddSlice<TallySlice>().AddBlazor();
            ducky(d);
            d.Use<HydrationRecorder>();
        });
    }

    // The store's clock: _time, counting the timer callbacks it runs, so a test can tell that a timer never fired.
    private sealed class CountingTimeProvider(FakeTimeProvider inner) : TimeProvider
    {
        private int _fired;

        public int Fired => Volatile.Read(ref _fired);

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        public override long GetTimestamp() => inner.GetTimestamp();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            inner.CreateTimer(
                s =>
                {
                    Interlocked.Increment(ref _fired);
                    callback(s);
                },
                state,
                dueTime,
                period);
    }
}

// The messages of the HydrationFailed terminals, in processing order.
internal sealed class HydrationFailures : System.Collections.Concurrent.ConcurrentQueue<string>;

internal sealed class HydrationFailureRecorder(HydrationFailures failures) : Middleware
{
    public override void AfterReduce(ActionContext context)
    {
        if (context.Action is HydrationFailed failed)
        {
            failures.Enqueue(failed.Message);
        }
    }
}
