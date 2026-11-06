using System.Text.Json;
using Ducky.Blazor;
using Ducky.ConcurrencyTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Ducky.Concurrency.Tests;

// SPEC §11.8, §17.3 row 21: INV-28. Jumps delivered on one thread while the drainer sends on another restore the entry
// their index names: the history is one immutable array only the drainer replaces, read once per jump. The deterministic
// twin is DevToolsInboundTests.DevTools_JumpConcurrentWithSend_RestoresMatchingEntry_Deterministic in Ducky.Blazor.Tests.
[Collection(nameof(Interleaving))]
public sealed class DevToolsInterleavingTests
{
    private const int Rounds = 20;

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task DevTools_JumpConcurrentWithSend_RestoresMatchingEntry(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var js = new Js();
        var seen = new Seen();
        await using var provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider())
            .AddSingleton<IJSRuntime>(js)
            .AddSingleton(seen)
            .AddDucky(static d => d.UseJson(DialJson.Default).AddSlice<DialSlice>()
                .AddBlazor(static o => o.IsBrowser = true)
                .UseDevTools(static o => o.MaxAge = 8)
                .Use<Probe>())
            .BuildServiceProvider();
        var store = (DuckyStore)provider.GetRequiredService<IStore>();
        var key = store.Slices.Single(static slice => slice.StateType == typeof(Dial)).Key;
        await Interleaving.Within(store.InitializeAsync(cancellationToken)); // 0 @@INIT, 1 StoreInitialized
        store.Restore(new Dictionary<string, object> { [key] = new Dial(100) }, Origin.Hydration); // 2: from here every entry's value is unique
        await Interleaving.Settled(store);
        var devTools = js.DevTools!;
        js.Sent.ShouldBe(2);

        // Each round, the sender's restore (sent, as a library origin it is never vetoed) races one jump to one of the three
        // latest entries the extension has seen. Within a round at most one more send lands, so none of them is evicted.
        using var round = new Barrier(2);
        List<long> targets = [];
        var sender = Task.Factory.StartNew(
            () =>
            {
                for (var i = 1; i <= Rounds; i++)
                {
                    round.SignalAndWait(cancellationToken);
                    store.Restore(new Dictionary<string, object> { [key] = new Dial(i) }, Origin.Hydration);
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var jumper = Task.Factory.StartNew(
            () =>
            {
                for (var i = 1; i <= Rounds; i++)
                {
                    round.SignalAndWait(cancellationToken);
                    var target = Math.Max(2, js.Sent - Random.Shared.Next(3));
                    targets.Add(target);
                    Interleaving.Wait(devTools.OnMessageAsync($$$"""{"type":"DISPATCH","payload":{"type":"JUMP_TO_ACTION","actionId":{{{target}}}}}""", i, false));
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await Interleaving.Within(Task.WhenAll(sender, jumper));
        await Interleaving.Settled(store);

        // Jumps restore in delivery order; each restored the value the extension was sent under the index it named.
        seen.Values.ShouldBe([.. targets.Select(js.ValueAt)]);
    }

    // Every DevTools restore the store reduced: the Dial it committed.
    private sealed class Seen
    {
        private readonly Lock _gate = new();
        private readonly List<int> _values = [];

        public IReadOnlyList<int> Values
        {
            get
            {
                lock (_gate)
                {
                    return [.. _values];
                }
            }
        }

        public void Add(int value)
        {
            lock (_gate)
            {
                _values.Add(value);
            }
        }
    }

    private sealed class Probe(Seen seen) : Middleware
    {
        public override void AfterReduce(ActionContext context)
        {
            if (context.Origin == Origin.DevTools)
            {
                seen.Add(context.State.Get<Dial>().Value);
            }
        }
    }

    // The JS runtime, ducky.js and the extension: devtoolsConnect connects, and the extension numbers what it receives, as
    // its lifted state does (@@INIT 0, then +1 per devtoolsSend), keeping each state's Dial.
    private sealed class Js : IJSRuntime, IJSObjectReference
    {
        private readonly Lock _gate = new();
        private readonly List<int> _values = [];

        public DevToolsMiddleware? DevTools { get; private set; }

        public int Sent
        {
            get
            {
                lock (_gate)
                {
                    return _values.Count - 1;
                }
            }
        }

        public int ValueAt(long index)
        {
            lock (_gate)
            {
                return _values[(int)index];
            }
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "import":
                    return new((TValue)(object)this);
                case "devtoolsConnect":
                    DevTools = ((DotNetObjectReference<DevToolsMiddleware>)args![1]!).Value;
                    return new((TValue)(object)true);
                case "devtoolsInit":
                    Keep((string)args![1]!);
                    return new(default(TValue)!);
                case "devtoolsSend":
                    Keep((string)args![2]!);
                    return new((TValue)(object)true);
                default:
                    return new(default(TValue)!);
            }
        }

        public ValueTask DisposeAsync() => default;

        private void Keep(string state)
        {
            using var document = JsonDocument.Parse(state);
            var value = document.RootElement.EnumerateObject().Single(static p => !p.Name.StartsWith('@')).Value.GetProperty("Value").GetInt32();
            lock (_gate)
            {
                _values.Add(value);
            }
        }
    }
}
