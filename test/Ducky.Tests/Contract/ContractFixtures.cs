// Actions, slices, effects and middleware for the core contract tests (SPEC §5.2, §7, §8.1; INV-10).
namespace Ducky.Tests.ContractFixtures;

internal sealed record Go(int Id);

internal sealed record Child(int Id);

internal sealed record Fail;

// Every reduced action in processing order. Fail always throws; Go and StoreInitialized throw while the switch is set, so
// a fault-injection run can make any reducer fail.
internal sealed record Journal(IReadOnlyList<object> Actions);

internal sealed class JournalSlice : Slice<Journal>
{
    public JournalSlice()
    {
        On<Go>(Add);
        On<Child>(Add);
        On<Fail>((Func<Journal, Journal>)(_ => throw new InvalidOperationException("reducer")));
        On<StoreInitialized>((state, action) => ThrowsOnInit ? throw new InvalidOperationException("init reducer") : Add(state, action));
    }

    public bool ThrowsGo { get; init; }

    public bool ThrowsOnInit { get; init; }

    // Runs inside the Go reducer, before it may throw: a test calls into the store from there.
    public Action? OnGo { get; set; }

    protected override Journal Initial => new([]);

    private Journal Add<TAction>(Journal state, TAction action)
        where TAction : notnull
    {
        if (action is Go)
        {
            OnGo?.Invoke();
            if (ThrowsGo)
            {
                throw new InvalidOperationException("Go reducer");
            }
        }

        return new([.. state.Actions, action]);
    }
}

internal enum Fault
{
    None,
    Sync,
    Async,
}

// One random failure injection of Api_AllAsyncMembers_NeverFault (§8.1): throwing reducers, hooks, effects, middleware
// DisposeAsync, and middleware InitializeAsync throwing synchronously or asynchronously.
internal sealed record Faults(
    bool Reducer,
    bool InitReducer,
    bool MayDispatch,
    bool BeforeReduce,
    bool AfterReduce,
    Fault Effect,
    Fault Init,
    Fault Dispose)
{
    public static readonly Faults All = new(true, true, true, true, true, Fault.Sync, Fault.Sync, Fault.Sync);

    public static readonly Faults AllAsync = new(true, true, true, true, true, Fault.Async, Fault.Async, Fault.Async);
}

// Throws from every hook the injection names.
internal sealed class FaultyMiddleware(Faults faults) : Middleware
{
    public override ValueTask InitializeAsync(CancellationToken cancellationToken) => faults.Init switch
    {
        Fault.Sync => throw new InvalidOperationException("init"),
        Fault.Async => ThrowLater("init"),
        _ => default,
    };

    // Go(1) is always admitted, so its effect runs and dispatches through its context under every injection.
    public override bool MayDispatch(ActionContext context) =>
        !faults.MayDispatch || context.Action is Go { Id: 1 } ? true : throw new InvalidOperationException("MayDispatch");

    public override void BeforeReduce(ActionContext context)
    {
        if (faults.BeforeReduce)
        {
            throw new InvalidOperationException("BeforeReduce");
        }
    }

    public override void AfterReduce(ActionContext context)
    {
        if (faults.AfterReduce)
        {
            throw new InvalidOperationException("AfterReduce");
        }
    }

#pragma warning disable CA2215 // justification: the base DisposeAsync does nothing, and the injection must throw synchronously
    public override ValueTask DisposeAsync() => faults.Dispose switch
    {
        Fault.Sync => throw new InvalidOperationException("dispose"),
        Fault.Async => ThrowLater("dispose"),
        _ => default,
    };
#pragma warning restore CA2215

    private static async ValueTask ThrowLater(string what)
    {
        await Task.Yield();
        throw new InvalidOperationException(what);
    }
}

// Admits nothing from the user: Local and Effect actions complete Vetoed (§6.4 step 4).
internal sealed class Veto : Middleware
{
    public override bool MayDispatch(ActionContext context) => false;
}

// On Go: dispatches Child(Id) through its EffectContext, keeps that task and the context, then throws as injected.
internal sealed class FaultyEffect(Fault fault) : Effect<Go>
{
    public List<Task<DispatchResult>> Dispatched { get; } = [];

    public override async Task Handle(Go action, EffectContext context, CancellationToken cancellationToken)
    {
        Dispatched.Add(context.DispatchAsync(new Child(action.Id)));
        if (fault == Fault.Sync)
        {
            throw new InvalidOperationException("effect");
        }

        await Task.Yield();
        if (fault == Fault.Async)
        {
            throw new InvalidOperationException("effect");
        }
    }
}

// A Merge or Switch effect on Go that hands its context out and parks until Release, so a test dispatches from a run that is
// superseded, or whose store is disposing. Its prefix dispatches Child(Id) when DispatchChild is set.
internal sealed class ParkedEffect(Concurrency policy) : Effect<Go>
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override Concurrency Policy => policy;

    public bool DispatchChild { get; init; }

    public Dictionary<int, EffectContext> Contexts { get; } = [];

    public Dictionary<int, Task<DispatchResult>> Children { get; } = [];

    public void Release() => _gate.TrySetResult();

    public override async Task Handle(Go action, EffectContext context, CancellationToken cancellationToken)
    {
        Contexts[action.Id] = context;
        if (DispatchChild)
        {
            Children[action.Id] = context.DispatchAsync(new Child(action.Id));
        }

        await _gate.Task.ConfigureAwait(false);
    }
}
