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
    private readonly Lock _gate = new();
    private readonly List<string> _entries = [];

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

    /// <summary>The store's init token, recorded by <see cref="HydrationRecorder"/>.</summary>
    public CancellationToken InitToken { get; set; }

    /// <summary>
    /// Completed with the thread that cancels the init token (an init abort), by a callback registered after
    /// PersistenceMiddleware's, so it runs just before that one.
    /// </summary>
    public TaskCompletionSource<Thread> AbortThread { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs on the processing thread right after each entry is added, inside the store's drain.</summary>
    public Action<string>? OnEntry { get; set; }

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
        var persistence = context.State.Get<PersistenceState>();
        log.Add($"{what} {persistence.Status}:{persistence.ScopeEpoch}");
    }
}

[JsonSerializable(typeof(Counter))]
[JsonSerializable(typeof(Tally))]
[JsonSerializable(typeof(Picky))]
[JsonSerializable(typeof(Unwritable))]
[JsonSerializable(typeof(EntityState<int, Todo>))]
[JsonSerializable(typeof(ImmutableList<Todo>))]
internal sealed partial class HydrationJson : JsonSerializerContext;
