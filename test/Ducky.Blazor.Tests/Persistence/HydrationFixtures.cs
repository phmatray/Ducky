using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ducky.Blazor.Tests.Core;

namespace Ducky.Blazor.Tests.Persistence;

internal sealed record Todo(int Id, string Title) : IEntity<int>;

// EntityState's constructor throws ArgumentException on duplicate ids, during deserialization too (§10).
internal sealed class TodosSlice : Slice<EntityState<int, Todo>>
{
    protected override EntityState<int, Todo> Initial => EntityState<int, Todo>.Empty;
}

internal sealed class PickySlice : Slice<Picky>
{
    protected override Picky Initial => new(0);
}

// Reads fine and can't be written back: its getter throws, so the local re-serialization fails.
internal sealed class Unwritable
{
    private readonly int _value;

    public int Value
    {
#pragma warning disable CA1065 // justification: the fixture exists to fail serialization from a getter
        get => throw new InvalidOperationException($"write-only ({_value})");
#pragma warning restore CA1065
        init => _value = value;
    }
}

internal sealed class UnwritableSlice : Slice<Unwritable>
{
    protected override Unwritable Initial => new();
}

// Throws on both hydration terminals: a throw on one dispatched with isFailure (HydrationFailed) is only logged, a throw on
// one dispatched normally (HydrationCompleted) is routed as a ReducerFailed (INV-12, §11.5 step 7).
internal sealed class CrashOnHydrationTerminalSlice : Slice<Crashes>
{
    public CrashOnHydrationTerminalSlice()
    {
        On<HydrationFailed>(static _ => throw new InvalidOperationException("reducer throws"));
        On<HydrationCompleted>(static _ => throw new InvalidOperationException("reducer throws"));
    }

    protected override Crashes Initial => new(0);
}

// One line per action the store processed, in processing order: what it was and the persistence state it left.
internal sealed class HydrationLog
{
    private readonly object _gate = new(); // not a Lock: Entries waits on it
    private readonly List<string> _entries = [];
    private bool _logging;

    /// <summary>
    /// What was logged, the action in progress included: an action is logged after its commit and its completion (§6.4 step 9),
    /// so a reader that saw either would otherwise race the recorder. Bounded: a drain a test holds on purpose only delays it.
    /// </summary>
    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_gate)
            {
                if (_logging)
                {
                    Monitor.Wait(_gate, TimeSpan.FromSeconds(10));
                }

                return [.. _entries];
            }
        }
    }

    /// <summary>Every hydration terminal action the store processed, in processing order.</summary>
    public ConcurrentQueue<object> Terminals { get; } = new();

    /// <summary>The store's init token, recorded by <see cref="HydrationRecorder"/>.</summary>
    public CancellationToken InitToken { get; set; }

    /// <summary>
    /// Completed with the thread that cancels the init token (an init abort), by a callback registered after
    /// PersistenceMiddleware's, so it runs just before that one.
    /// </summary>
    public TaskCompletionSource<Thread> AbortThread { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs on the processing thread right after each entry is added, inside the store's drain.</summary>
    public Action<string>? OnEntry { get; set; }

    /// <summary>On the drainer, before the action can commit or complete.</summary>
    public void Begin()
    {
        lock (_gate)
        {
            _logging = true;
        }
    }

    /// <summary>On the drainer: logs the action <see cref="Begin"/> announced.</summary>
    public void AddRecorded(string entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
            _logging = false;
            Monitor.PulseAll(_gate);
        }

        OnEntry?.Invoke(entry);
    }

    public void Add(string entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }

        OnEntry?.Invoke(entry);
    }
}

// Registered after AddBlazor: sees every action, the restores and the terminals included, after its reduce.
internal sealed class HydrationRecorder(HydrationLog log) : Middleware
{
    public override ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        log.InitToken = cancellationToken;
        cancellationToken.UnsafeRegister(static l => ((HydrationLog)l!).AbortThread.TrySetResult(Thread.CurrentThread), log);
        return default;
    }

    public override void BeforeReduce(ActionContext context) => log.Begin();

    public override void AfterReduce(ActionContext context)
    {
        var what = context.Action switch
        {
            StoreInitialized => "initialized",
            HydrationCompleted completed => $"completed:{completed.StateRestored}:{completed.ScopeEpoch}:{context.Origin}",
            HydrationFailed failed => $"failed:{failed.ErrorType}:{failed.ScopeEpoch}:{context.Origin}",
            _ when context.Origin == Origin.Hydration => $"restore:{string.Join(",", context.ChangedKeys)}",
            _ => context.ActionType,
        };
        if (context.Action is HydrationCompleted or HydrationFailed)
        {
            log.Terminals.Enqueue(context.Action);
        }

        var persistence = context.State.Get<PersistenceState>();
        log.AddRecorded($"{what} {persistence.Status}:{persistence.ScopeEpoch}");
    }
}

[JsonSerializable(typeof(Counter))]
[JsonSerializable(typeof(Tally))]
[JsonSerializable(typeof(Picky))]
[JsonSerializable(typeof(Unwritable))]
[JsonSerializable(typeof(EntityState<int, Todo>))]
[JsonSerializable(typeof(ImmutableList<Todo>))]
internal sealed partial class HydrationJson : JsonSerializerContext;
