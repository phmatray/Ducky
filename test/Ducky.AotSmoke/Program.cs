using Ducky;
using Microsoft.Extensions.DependencyInjection;

// SPEC §10: one PASS <name>/FAIL <name> line per named assertion, exit code 1 on any FAIL. Every name is a manifest name
// mapped to Ducky.AotSmoke (the AotSmoke target compares the PASS set with the active entries): area stories extend the
// existing assertions rather than print new names (PLAN P8).
var failed = false;

// AotSmoke (INV-24): the core after ILC. Dispatch/reduce, the four effect policies (an Effect<T> and an EffectGroup, both
// activated by DI), IStore.Select and its typed overload, and SelectorDef with and without an argument (M3-05).
await CheckAsync("AotSmoke", async () =>
{
    if (new ActionTypeAttribute("smoke/started").Name != "smoke/started")
    {
        return false;
    }

    await using var provider = new ServiceCollection()
        .AddSingleton<Journal>()
        .AddDucky(d => d.AddSlice<CounterSlice>().AddEffect<MergeEffect>().AddEffect<PolicyEffects>())
        .BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var store = scope.ServiceProvider.GetRequiredService<IStore>();
    var journal = provider.GetRequiredService<Journal>();
    await store.InitializeAsync();

    List<int> changes = [];
    using var count = store.Select(s => s.Get<Counter>().Count, changes.Add);
    using var typed = store.Select<Counter, int>(c => c.Count);
    var doubled = Selector.Define(s => s.Get<Counter>().Count, c => c * 2);
    var plus = Selector.Define(s => s.Get<Counter>().Count, (int c, int arg) => c + arg);

    var reduced = await store.DispatchAsync(new Increment()) == DispatchResult.Reduced
        && store.State.Get<Counter>().Count == 1 && count.Value == 1 && typed.Value == 1 && changes is [1]
        && doubled.Evaluate(store.State) == 2 && doubled.Create()(store.State) == 2
        && plus.Evaluate(store.State, 10) == 11 && plus.Create(() => 20)(store.State) == 21;

    foreach (var fired in (object[])[new MergeFired(), new SwitchFired(), new ExhaustFired(), new QueueFired()])
    {
        await store.DispatchAsync(fired);
        await store.DispatchAsync(fired);
    }

    // While the gate is shut: Merge runs both, Switch runs both and cancels the first, Exhaust drops the second and Queue
    // holds it until the first completes.
    var running = journal.Runs(Concurrency.Merge) is [{ IsCancellationRequested: false }, { IsCancellationRequested: false }]
        && journal.Runs(Concurrency.Switch) is [{ IsCancellationRequested: true }, { IsCancellationRequested: false }]
        && journal.Runs(Concurrency.Exhaust) is [_]
        && journal.Runs(Concurrency.Queue) is [_];
    journal.Gate.SetResult();
    await store.WhenIdleAsync();

    // MergeEffect dispatches MergeDone per run: two more reductions.
    return reduced && running && journal.Runs(Concurrency.Exhaust) is [_] && journal.Runs(Concurrency.Queue) is [_, _]
        && store.State.Get<Counter>().Count == 3 && changes is [1, 2, 3];
});

return failed ? 1 : 0;

async Task CheckAsync(string name, Func<Task<bool>> assertion)
{
    bool passed;
    try
    {
        passed = await assertion();
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        passed = false;
    }
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}");
    failed |= !passed;
}

internal sealed record Counter(int Count);

internal sealed record Increment;

internal sealed record MergeFired;

internal sealed record MergeDone;

internal sealed record SwitchFired;

internal sealed record ExhaustFired;

internal sealed record QueueFired;

internal sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice()
    {
        On<Increment>(c => c with { Count = c.Count + 1 });
        On<MergeDone>(c => c with { Count = c.Count + 1 });
    }

    protected override Counter Initial => new(0);
}

// Each run records its token, then waits for the gate.
internal sealed class Journal
{
    private readonly Dictionary<Concurrency, List<CancellationToken>> _runs = [];

    public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<CancellationToken> Runs(Concurrency policy)
    {
        lock (_runs)
        {
            return [.. _runs.GetValueOrDefault(policy, [])];
        }
    }

    public Task Run(Concurrency policy, CancellationToken token)
    {
        lock (_runs)
        {
            _runs.TryAdd(policy, []);
            _runs[policy].Add(token);
        }
        return Gate.Task;
    }
}

internal sealed class MergeEffect(Journal journal) : Effect<MergeFired>
{
    public override async Task Handle(MergeFired action, EffectContext context, CancellationToken cancellationToken)
    {
        await journal.Run(Policy, cancellationToken);
        context.Dispatch(new MergeDone());
    }
}

internal sealed class PolicyEffects : EffectGroup
{
    public PolicyEffects(Journal journal)
    {
        On<SwitchFired>((_, _, token) => journal.Run(Concurrency.Switch, token), Concurrency.Switch);
        On<ExhaustFired>((_, _, token) => journal.Run(Concurrency.Exhaust, token), Concurrency.Exhaust);
        On<QueueFired>((_, _, token) => journal.Run(Concurrency.Queue, token), Concurrency.Queue);
    }
}
