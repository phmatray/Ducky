using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.DevTools;

internal sealed record Label(string Text);

internal sealed record SetLabel(string Text);

internal sealed class LabelSlice : Slice<Label>
{
    public LabelSlice() => On<SetLabel>(static (_, action) => new(action.Text));

    protected override Label Initial => new("");
}

// A polymorphic state (§11.8): only its declared type Bird carries the $type discriminator; Hatch swaps the derived type.
[JsonPolymorphic]
[JsonDerivedType(typeof(Goose), "goose")]
[JsonDerivedType(typeof(Duckling), "duckling")]
internal abstract record Bird;

internal sealed record Goose(int Honks) : Bird;

internal sealed record Duckling(string Name) : Bird;

internal sealed record Hatch(string Name);

internal sealed class BirdSlice : Slice<Bird>
{
    public BirdSlice() => On<Hatch>(static (_, action) => new Duckling(action.Name));

    public override string Key => "bird";

    protected override Bird Initial => new Goose(3);
}

// An action the app's resolver has no type info for: its DevTools payload degrades to null (§10).
internal sealed record Opaque(int Value);

internal sealed record Explode;

internal sealed record Blast;

internal sealed record Fuse(int Length);

// Its reducer throws: the store routes a ReducerFailed (§6.4).
internal sealed class ExplodingSlice : Slice<Fuse>
{
    public ExplodingSlice() => On<Explode>(static _ => throw new InvalidOperationException("reducer boom"));

    public override string Key => "exploding";

    protected override Fuse Initial => new(0);
}

// Its handler throws: the store routes an EffectFailed (§6.6).
internal sealed class ExplodingEffect : Effect<Blast>
{
    public override Task Handle(Blast action, EffectContext context, CancellationToken cancellationToken) =>
        throw new ArgumentException("effect boom");
}

// A late effect result (§11.8): on SetLabel, waits for the gate, then reports what its Increment dispatch came to.
internal sealed class LateIncrement(TaskCompletionSource gate, TaskCompletionSource<DispatchResult> result) : Effect<SetLabel>
{
    public override async Task Handle(SetLabel action, EffectContext context, CancellationToken cancellationToken)
    {
        await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false); // a disposal ends the wait
        result.SetResult(await context.DispatchAsync(new Increment()).ConfigureAwait(false));
    }
}

// Holds the store's init open until the test answers "hold" (DevToolsHarness.OnHold): restores reduced meanwhile reach
// AfterReduce before StoreInitialized, as persistence's storage restore does (§6 "Hooks versus init").
internal sealed class HoldInit(IJSRuntime js) : Middleware
{
    public override async ValueTask InitializeAsync(CancellationToken cancellationToken) =>
        await js.InvokeAsync<object?>("hold", cancellationToken, []).ConfigureAwait(false);
}

// The app's resolver: its states and actions, none of Ducky's or Ducky.Blazor's own types.
[JsonSerializable(typeof(Counter))]
[JsonSerializable(typeof(Label))]
[JsonSerializable(typeof(Fuse))]
[JsonSerializable(typeof(Increment))]
[JsonSerializable(typeof(SetLabel))]
[JsonSerializable(typeof(Explode))]
[JsonSerializable(typeof(Blast))]
[JsonSerializable(typeof(Bird))]
[JsonSerializable(typeof(Hatch))]
internal sealed partial class DevToolsJson : JsonSerializerContext;

// One devtoolsSend call: its id, and the action and state it carried, parsed.
internal sealed record Sent(string Id, JsonObject Action, JsonObject State);

// One store with UseDevTools over a FakeJsRuntime standing for ducky.js and the extension (§11.8: the exact strings that
// cross the boundary are recorded): devtoolsConnect answers OnConnect, devtoolsSend OnSend.
internal sealed class DevToolsHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    public DevToolsHarness(Action<DuckyBuilder> configure, bool browser = true, Action<BlazorOptions>? blazor = null)
    {
        OnImport = () => Js;
        Js.Respond = (identifier, args) => identifier switch
        {
            "import" => OnImport(),
            "devtoolsConnect" => OnConnect(args),
            "devtoolsInit" => OnInit(args),
            "devtoolsSend" => OnSend(args),
            "devtoolsDisconnect" => OnDisconnect(args),
            "hold" => OnHold(),
            _ => null,
        };

        var collection = new ServiceCollection();
        collection.AddSingleton<IJSRuntime>(Js).AddSingleton<TimeProvider>(Time);
        collection.AddLogging(logging => logging.AddProvider(new FakeLoggerProvider(Logs)).SetMinimumLevel(LogLevel.Debug));
        collection.AddDucky(d =>
        {
            d.UseJson(DevToolsJson.Default).AddSlice<CounterSlice>().AddSlice<LabelSlice>();
            d.AddBlazor(o =>
            {
                o.IsBrowser = browser;
                blazor?.Invoke(o);
            });
            configure(d);
        });
        _provider = collection.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();
    }

    public FakeJsRuntime Js { get; } = new();

    public FakeTimeProvider Time { get; } = new();

    public FakeLogCollector Logs { get; } = new();

    public Func<object?> OnImport { get; set; }

    public Func<object?[], object?> OnConnect { get; set; } = static _ => true;

    public Func<object?[], object?> OnInit { get; set; } = static _ => null;

    public Func<object?[], object?> OnSend { get; set; } = static _ => true;

    public Func<object?[], object?> OnDisconnect { get; set; } = static _ => null;

    public Func<object?> OnHold { get; set; } = static () => null;

    public IStore Store => _scope.ServiceProvider.GetRequiredService<IStore>();

    /// <summary>The store's id (§11.7), the one every JS registration names.</summary>
    public string StoreId => _scope.ServiceProvider.GetRequiredService<PersistenceSlice>().Id;

    public IReadOnlyList<string> Identifiers => [.. Js.Calls.Select(static call => call.Identifier)];

    public object?[] Connect => Js.Calls.Single(static call => call.Identifier == "devtoolsConnect").Args;

    /// <summary>The DevTools middleware, reached through the reference it handed to devtoolsConnect.</summary>
    public DevToolsMiddleware Middleware => ((DotNetObjectReference<DevToolsMiddleware>)Connect[1]!).Value;

    /// <summary>Every devtoolsInit call: the id and the parsed @@INIT state.</summary>
    public IReadOnlyList<(string Id, JsonObject State)> Inits =>
        [.. Js.Calls.Where(static call => call.Identifier == "devtoolsInit").Select(static call => ((string)call.Args[0]!, Parse((string)call.Args[1]!)))];

    public IReadOnlyList<Sent> Sends =>
        [.. Js.Calls.Where(static call => call.Identifier == "devtoolsSend").Select(static call => new Sent((string)call.Args[0]!, Parse((string)call.Args[1]!), Parse((string)call.Args[2]!)))];

    /// <summary>The FSA types sent, in send order.</summary>
    public IReadOnlyList<string> SentTypes => [.. Sends.Select(static sent => (string)sent.Action["type"]!)];

    public Task InitializeAsync() => Store.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

    public IEnumerable<int> EventIds(LogLevel level) => Logs.GetSnapshot().Where(record => record.Level == level).Select(static record => record.Id.Id);

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();
}
