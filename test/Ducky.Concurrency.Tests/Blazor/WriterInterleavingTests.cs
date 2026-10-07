using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsCheck;
using Ducky.Blazor;
using Ducky.ConcurrencyTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

// Outside Ducky.Concurrency.Tests, as in RestoreInterleavingTests: STJ's generated code names its types by full name.
namespace Ducky.ConcurrencyTests
{
    internal sealed record Dial(int Value);

    internal sealed record SetDial(int Value);

    internal sealed class DialSlice : Slice<Dial>
    {
        public DialSlice() => On<SetDial>(static (_, action) => new(action.Value));

        protected override Dial Initial => new(0);
    }

    [JsonSerializable(typeof(Dial))]
    internal sealed partial class DialJson : JsonSerializerContext;
}

namespace Ducky.Concurrency.Tests
{
    // SPEC §11.5 "A skipped key stays dirty", §17.3 row 6: INV-16. The writer's hydration skip races the terminal.
    [Collection(nameof(Interleaving))]
    public sealed class WriterInterleavingTests
    {
        private const string StorageKey = "ducky:dial";

        // System-origin changes during hydration (user dispatches wait in the init buffer until StoreInitialized) race the
        // completion of the read, so the terminal can commit between a writer's snapshot read and its add to _deferred, or
        // between the add and its recheck. Whatever the interleaving, once everything settles the stored value is the final
        // state: a deferred key is never stranded. Its deterministic twins, one per AfterDeferHook point, are
        // WriterDeferTests.Write_TerminalBetweenReadAndDefer_… and Write_TerminalAfterDeferAdd_… in Ducky.Blazor.Tests.
        [Theory]
        [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
        public async Task Write_SkipRacesTerminal_DeferredKeyStillWritten(int repeat)
        {
            _ = repeat;
            var change = Gen.Int[1, 1000].Operation<Actual, Model>(v => $"DispatchSystem(SetDial({v}))", (a, v) => a.Change(v), (_, _) => { });
            var complete = Gen.Const(0).Operation<Actual, Model>(_ => "CompleteRead", (a, _) => a.CompleteRead(), (_, _) => { });

            // Bounded by progress (Interleaving.WithinProperty): every blocking step goes through Interleaving.Wait.
            await Interleaving.WithinProperty(Task.Run(
                () => Gen.Const(() => (new Actual(), new Model())).SampleParallel(
                    change,
                    complete,
                    equal: static (a, _) => a.SettledWrittenFinalState,
                    maxSequentialOperations: 4,
                    maxParallelOperations: 4),
                TestContext.Current.CancellationToken));
        }

        private sealed class Actual
        {
            private readonly TaskCompletionSource<object?> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly ConcurrentDictionary<string, string> _stored = new();
            private readonly DuckyStore _store;
            private readonly Line _line = new();
            private readonly Task _init;
            private bool? _settled;

            public Actual()
            {
                // Never disposed: the sample's store dies with it, as in SubscriptionCoreInterleavingTests.
                var provider = new ServiceCollection()
                    .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero)))
                    .AddSingleton<IJSRuntime>(new Js(_read.Task, _stored))
                    .AddSingleton(_line)
                    .AddDucky(static d =>
                    {
                        d.UseJson(DialJson.Default).AddSlice<DialSlice>()
                            .AddBlazor(static o =>
                            {
                                o.IsBrowser = true;

                                // Jitter only, it orders nothing: the gap between the writer's snapshot read and its add
                                // (BeforeAdd), and between the add and its recheck (AfterAdd), is a few instructions, so
                                // without it the terminal almost never commits inside either. The hook-driven, ordered
                                // tests are the deterministic twins.
                                o.AfterDeferHook = static (_, _) => Thread.SpinWait(Random.Shared.Next(2000));
                            })
                            .Persist<DialSlice>();
                        d.Use<Sender>();
                    })
                    .BuildServiceProvider();
                _store = (DuckyStore)provider.GetRequiredService<IStore>();
                _init = _store.InitializeAsync(TestContext.Current.CancellationToken);
            }

            // Once, after the run (equal may be called once per linearization tried).
            public bool SettledWrittenFinalState => _settled ??= Settle();

            public void Change(int value) => _line.Send!(new SetDial(value), false);

            public void CompleteRead() => _read.TrySetResult(null);

            public override string ToString() =>
                $"state {_store.State.Get<Dial>().Value}, stored {_stored.GetValueOrDefault(StorageKey) ?? "nothing"}";

            // Every change has been reduced once the drain exits; the writer of the final change, or the release of the
            // deferred key, then writes the final state. A stranded key never does, and the wait fails the run.
            private bool Settle()
            {
                CompleteRead();
                Interleaving.Wait(_init);
                Interleaving.Wait(Interleaving.Settled(_store));
                var final = JsonSerializer.Serialize(_store.State.Get<Dial>(), DialJson.Default.Dial);
                // Nothing stored stands for the initial state: a run without a change has nothing to write.
                var initial = JsonSerializer.Serialize(new Dial(0), DialJson.Default.Dial);
                Until(() => (Stored() ?? initial) == final);
                return true;
            }

            private string? Stored() =>
                _stored.TryGetValue(StorageKey, out var envelope)
                    ? JsonDocument.Parse(envelope).RootElement.GetProperty("s").GetRawText()
                    : null;

            // Progress-bounded (Interleaving.Wait), spun on a dedicated thread.
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
        }

        private sealed class Model
        {
            public override string ToString() => "final state stored";
        }

        // A System-origin change at any moment, hydration included (DispatchSystem bypasses the init buffer).
        private sealed class Sender : Middleware
        {
            public Sender(Line line) => line.Send = DispatchSystem;
        }

        private sealed class Line
        {
            public Action<object, bool>? Send { get; set; }
        }

        // The JS runtime and the ducky.js module it imports: every storageGet answers with the one pending read, and
        // storageSet stores the envelope.
        private sealed class Js(Task<object?> read, ConcurrentDictionary<string, string> stored) : IJSRuntime, IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                switch (identifier)
                {
                    case "import":
                        return new((TValue)(object)this);
                    case "storageGet":
                        return new(As<TValue>(read));
                    case "storageSet":
                        stored[(string)args![1]!] = (string)args[2]!;
                        return new((TValue)(object)true);
                    default:
                        return new(default(TValue)!);
                }
            }

            public ValueTask DisposeAsync() => default;

            private static async Task<TValue> As<TValue>(Task<object?> value) => (TValue)(await value)!;
        }
    }
}
