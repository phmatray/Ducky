using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Ducky;
using Ducky.Blazor;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// SPEC §10: one PASS <name>/FAIL <name> line per named assertion, exit code 1 on any FAIL. Every name is a manifest name
// mapped to Ducky.AotSmoke (the AotSmoke target compares the PASS set with the active entries): area stories extend the
// existing assertions rather than print new names (PLAN P8). The arguments are the manifest names whose stage is not yet
// active (P1): their assertions run, and a FAIL still prints and fails, but a PASS stays silent until the bump.
var failed = false;
var inactive = args.ToHashSet(StringComparer.Ordinal);

// AotSmoke (INV-24): the core after ILC. Dispatch/reduce, the four effect policies (an Effect<T> and an EffectGroup, both
// activated by DI), IStore.Select and its typed overload, and SelectorDef with and without an argument (M3-05), SliceStore and
// EntityState (M9-06), and a PersistStorage.Server round-trip (M12-05).
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
        && store.State.Get<Counter>().Count == 3 && changes is [1, 2, 3]
        && await SliceStoreAndEntityStateAsync() && await ServerPersistenceRoundTripAsync();
});

// [ActionType] read at run time, after ILC, as ActionContext.ActionType (M4-07, §5.8) and as the DevTools FSA type (M12-05).
await CheckAsync("ActionType_AttributeName_SurvivesAot", async () =>
{
    var js = new FakeDevToolsJs();
    await using var provider = new ServiceCollection()
        .AddSingleton<SeenTypes>()
        .AddSingleton<IJSRuntime>(js)
        .AddDucky(d => d.AddSlice<CounterSlice>().Use<TypeRecorder>().UseJson(SmokeJson.Default).AddBlazor().UseDevTools())
        .BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var store = scope.ServiceProvider.GetRequiredService<IStore>();
    await store.InitializeAsync();

    await store.DispatchAsync(new TodoAdded("milk"));
    await store.WhenIdleAsync();

    return provider.GetRequiredService<SeenTypes>().Names is [.., "todos/added"]
        && js.Sends.Any(static sent => sent.Action["type"]?.GetValue<string>() == "todos/added");
});

// DevTools (INV-23, M12-05): UseDevTools over a fake ducky.js whose import succeeds, so the gate is Interactive, and an
// extension that is present. After ILC, with Trace on, the todos/added FSA carries its payload and a stack (possibly of
// <unknown> frames), and the state its slices.
await CheckAsync("DevToolsPayload_NonEmpty", async () =>
{
    var js = new FakeDevToolsJs();
    await using var provider = new ServiceCollection()
        .AddSingleton<IJSRuntime>(js)
        .AddDucky(d => d.AddSlice<CounterSlice>().UseJson(SmokeJson.Default).AddBlazor().UseDevTools(static o => o.Trace = true))
        .BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var store = scope.ServiceProvider.GetRequiredService<IStore>();
    await store.InitializeAsync();

    await store.DispatchAsync(new TodoAdded("milk"));
    await store.WhenIdleAsync();

    var counterKey = new CounterSlice().Key;
    return js.Sends.SingleOrDefault(static sent => sent.Action["type"]?.GetValue<string>() == "todos/added") is { } todo
        && todo.Action["payload"]?["Text"]?.GetValue<string>() == "milk"
        && todo.Action["stack"]?.GetValue<string>() is { Length: > 0 }
        && todo.State[counterKey]?["Count"]?.GetValue<int>() == 0;
});

return failed ? 1 : 0;

// PersistStorage.Server over MemoryDistributedCache, keyed {KeyPrefix}:{Scope}:{sliceKey}: the first store writes the
// slice through and flushes on dispose; a second store of the same app reads it back during init (a Hydration restore).
static async Task<bool> ServerPersistenceRoundTripAsync()
{
    await using var provider = new ServiceCollection()
        .AddDistributedMemoryCache()
        .AddDucky(d => d.AddSlice<CounterSlice>().UseJson(SmokeJson.Default)
            .Persist<CounterSlice>(static o => o.Storage = PersistStorage.Server)
            .AddBlazor(static o =>
            {
                o.KeyPrefix = "smoke";
                o.Scope = static (_, _) => new("smoke");
            }))
        .BuildServiceProvider();
    await using (var writer = provider.CreateAsyncScope())
    {
        var store = writer.ServiceProvider.GetRequiredService<IStore>();
        await store.InitializeAsync();
        await store.DispatchAsync(new Increment());
    }

    var stored = await provider.GetRequiredService<IDistributedCache>().GetStringAsync($"smoke:smoke:{new CounterSlice().Key}");
    await using var reader = provider.CreateAsyncScope();
    var restored = reader.ServiceProvider.GetRequiredService<IStore>();
    await restored.InitializeAsync();

    return stored is not null && restored.State.Get<Counter>().Count == 1 && restored.State.WasRestored<Counter>();
}

// The injected SliceStore is the store-owned slice: Set and SetAsync reduce EntityState operations through the store, and
// its state round-trips through the UseJson context into a second store's Hydration restore.
static async Task<bool> SliceStoreAndEntityStateAsync()
{
    await using var provider = new ServiceCollection()
        .AddDucky(d => d.AddSlice<TodoStore>().UseJson(SmokeJson.Default))
        .BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var store = scope.ServiceProvider.GetRequiredService<IStore>();
    var todos = scope.ServiceProvider.GetRequiredService<TodoStore>();

    todos.Add(new Todo(1, "milk"));
    todos.Add(new Todo(2, "eggs"));
    var toggled = await todos.ToggleAsync(1) == DispatchResult.Reduced;
    var state = todos.State;
    var entities = toggled && ReferenceEquals(state, store.State.Get<EntityState<int, Todo>>())
        && state is { Count: 2, Items: [{ Id: 1, Done: true }, { Id: 2, Done: false }] }
        && state.Find(2)?.Text == "eggs" && !state.Contains(3) && ReferenceEquals(state.Remove(3), state);

    if (!store.Json.TrySerialize(state, typeof(EntityState<int, Todo>), out var json))
    {
        return false;
    }

    await using var restoredProvider = new ServiceCollection()
        .AddDucky(d => d.AddSlice<TodoStore>().UseJson(SmokeJson.Default))
        .BuildServiceProvider();
    await using var restoredScope = restoredProvider.CreateAsyncScope();
    var restoredStore = restoredScope.ServiceProvider.GetRequiredService<IStore>();
    using (var document = JsonDocument.Parse(json))
    {
        restoredStore.Restore(new Dictionary<string, object> { [todos.Key] = document.RootElement }, Origin.Hydration);
    }
    await restoredStore.WhenIdleAsync();

    return entities && restoredStore.State.WasRestored(todos.Key)
        && restoredScope.ServiceProvider.GetRequiredService<TodoStore>().State is
        { Items: [{ Id: 1, Text: "milk", Done: true }, { Id: 2, Text: "eggs", Done: false }] } restored
        && restored.Find(1)?.Text == "milk";
}

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
    if (!passed || !inactive.Contains(name))
    {
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}");
    }
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

[ActionType("todos/added")]
internal sealed record TodoAdded(string Text);

internal sealed class SeenTypes
{
    public List<string> Names { get; } = [];
}

internal sealed class TypeRecorder(SeenTypes seen) : Middleware
{
    public override void AfterReduce(ActionContext context) => seen.Names.Add(context.ActionType);
}

internal sealed record Todo(int Id, string Text, bool Done = false) : IEntity<int>;

internal sealed class TodoStore : SliceStore<EntityState<int, Todo>>
{
    protected override EntityState<int, Todo> Initial => EntityState<int, Todo>.Empty;

    public void Add(Todo todo) => Set(s => s.Add(todo));

    public Task<DispatchResult> ToggleAsync(int id) => SetAsync(s => s.Update(id, t => t with { Done = !t.Done }));
}

[JsonSerializable(typeof(EntityState<int, Todo>))]
[JsonSerializable(typeof(Counter))]
[JsonSerializable(typeof(TodoAdded))]
internal sealed partial class SmokeJson : JsonSerializerContext;

// ducky.js and the Redux DevTools extension: the import returns this module, devtoolsConnect finds the extension, and each
// devtoolsSend (id, action FSA, state) is recorded parsed and answered as known; any other call returns its default.
internal sealed class FakeDevToolsJs : IJSRuntime, IJSObjectReference
{
    // JSInterop's annotation on every InvokeAsync result type: what System.Text.Json keeps under trimming.
    private const DynamicallyAccessedMemberTypes JsonSerialized =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties;

    private readonly Lock _gate = new();
    private readonly List<(JsonObject Action, JsonObject State)> _sends = [];

    public List<(JsonObject Action, JsonObject State)> Sends
    {
        get
        {
            lock (_gate)
            {
                return [.. _sends];
            }
        }
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(JsonSerialized)] TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(JsonSerialized)] TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        object? answer = identifier switch
        {
            "import" => this,
            "devtoolsConnect" => true,
            "devtoolsSend" => Record((string)args![1]!, (string)args[2]!),
            _ => null,
        };
        return new(answer is TValue value ? value : default!);
    }

    public ValueTask DisposeAsync() => default;

    private bool Record(string action, string state)
    {
        lock (_gate)
        {
            _sends.Add((JsonNode.Parse(action)!.AsObject(), JsonNode.Parse(state)!.AsObject()));
        }
        return true;
    }
}
