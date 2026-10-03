// Middleware for MiddlewareTests (SPEC §5.6, §6.4, §6.11). Every hook and DisposeAsync call is written to a shared
// journal as "{name}.{Hook} {ActionType name}", so a test asserts the order across middleware.
namespace Ducky.Tests.MiddlewareFixtures;

internal sealed class Recorder(string name, List<string> journal) : Middleware
{
    public Func<ActionContext, bool> Allow { get; set; } = _ => true;

    public Action<ActionContext>? OnBefore { get; set; }

    public Action<ActionContext>? OnAfter { get; set; }

    public Func<ValueTask>? OnDispose { get; set; }

    // InitializeAsync runs this delegate, when set, with the init token.
    public Func<CancellationToken, ValueTask>? OnInit { get; set; }

    public List<ActionContext> Contexts { get; } = [];

    public IStore AttachedStore => Store;

    public TimeSpan AttachedDisposeTimeout => DisposeTimeout;

    public void System(object action, bool isFailure = false) => DispatchSystem(action, isFailure);

    public override ValueTask InitializeAsync(CancellationToken cancellationToken) => OnInit?.Invoke(cancellationToken) ?? default;

    public override bool MayDispatch(ActionContext context)
    {
        Record(nameof(MayDispatch), context);
        return Allow(context);
    }

    public override void BeforeReduce(ActionContext context)
    {
        Record(nameof(BeforeReduce), context);
        OnBefore?.Invoke(context);
    }

    public override void AfterReduce(ActionContext context)
    {
        Record(nameof(AfterReduce), context);
        OnAfter?.Invoke(context);
    }

#pragma warning disable CA2215 // justification: the base DisposeAsync does nothing, and OnDispose must be able to throw synchronously
    public override ValueTask DisposeAsync()
    {
        journal.Add($"{name}.{nameof(DisposeAsync)}");
        return OnDispose?.Invoke() ?? default;
    }
#pragma warning restore CA2215

    private void Record(string hook, ActionContext context)
    {
        Contexts.Add(context);
        journal.Add($"{name}.{hook} {context.Action.GetType().Name}");
    }
}

// Overrides nothing: the default hooks admit every action and observe nothing.
internal sealed class Bare : Middleware;

// Resolved from DI by the Use<T> tests: each instance counts its construction and records AfterReduce in the journal.
internal sealed class Journal
{
    public List<string> Entries { get; } = [];

    public List<FirstMiddleware> Instances { get; } = [];
}

internal sealed class FirstMiddleware : Middleware
{
    private readonly Journal _journal;

    public FirstMiddleware(Journal journal)
    {
        _journal = journal;
        journal.Instances.Add(this);
    }

    // Store and DisposeTimeout as read in the first hook (BeforeReduce of StoreInitialized): attached before any hook runs.
    public (IStore Store, TimeSpan DisposeTimeout)? AtFirstHook { get; private set; }

    public override void BeforeReduce(ActionContext context) => AtFirstHook ??= (Store, DisposeTimeout);

    public override void AfterReduce(ActionContext context) => _journal.Entries.Add($"First {context.Action.GetType().Name}");
}

// A scoped service: resolving it from the root provider throws under scope validation.
internal sealed class ScopedDependency;

internal sealed class SecondMiddleware(Journal journal, ScopedDependency dependency) : Middleware
{
    public ScopedDependency Dependency { get; } = dependency;

    public override void AfterReduce(ActionContext context) => journal.Entries.Add($"Second {context.Action.GetType().Name}");
}
