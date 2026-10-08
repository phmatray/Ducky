using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Persistence;

internal sealed record Level(double Value);

internal sealed record SetLevel(double Value);

// A double, so a NaN fails serialization (§10).
internal sealed class LevelSlice : Slice<Level>
{
    public LevelSlice() => On<SetLevel>(static (_, action) => new(action.Value));

    protected override Level Initial => new(0);
}

internal sealed record Dial(int Value);

internal sealed record SetDial(int Value);

internal sealed class DialSlice : Slice<Dial>
{
    public DialSlice() => On<SetDial>(static (_, action) => new(action.Value));

    protected override Dial Initial => new(0);
}

internal sealed record Crashes(int Count);

// Throws on PersistenceFailed: dispatched with isFailure, that throw is logged, never routed as a ReducerFailed (INV-12).
internal sealed class CrashOnPersistenceFailedSlice : Slice<Crashes>
{
    public CrashOnPersistenceFailedSlice() => On<PersistenceFailed>(static _ => throw new InvalidOperationException("reducer throws"));

    protected override Crashes Initial => new(0);
}

// Every PersistenceFailed the store processed, in order.
internal sealed class FailureInbox
{
    public Channel<PersistenceFailed> Failures { get; } = Channel.CreateUnbounded<PersistenceFailed>();
}

internal sealed class FailureTap(FailureInbox inbox) : Middleware
{
    public override void AfterReduce(ActionContext context)
    {
        if (context.Action is PersistenceFailed failed)
        {
            inbox.Failures.Writer.TryWrite(failed);
        }
    }
}

// Lets a test raise a System-origin change (a DispatchSystem from another middleware) at any moment, init included.
internal sealed class SystemSender : Middleware
{
    public SystemSender(SystemLine line) => line.Send = DispatchSystem;
}

internal sealed class SystemLine
{
    public Action<object, bool>? Send { get; set; }
}

// Registered before AddBlazor: dispatches during its own init, before persistence's synchronous prefix has run.
internal sealed class EarlyChange : Middleware
{
    public override ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        DispatchSystem(new SetLevel(2));
        DispatchSystem(new Increment());
        return default;
    }
}

internal sealed record StartHung;

// A run that ignores its token and never ends (a listener on a non-cancellable read, §6.11 5a).
internal sealed class HungEffect : Effect<StartHung>
{
    public override Task Handle(StartHung action, EffectContext context, CancellationToken cancellationToken) => new TaskCompletionSource().Task;
}

// Its init ignores the token and never ends (the supported case of Init_HangingMiddleware_TimesOutAndReleasesBuffer).
internal sealed class HangingInit : Middleware
{
    public override ValueTask InitializeAsync(CancellationToken cancellationToken) => new(new TaskCompletionSource().Task);
}

// Scopes every Meter it creates to itself, as the framework's factory does, so a listener can pick this store's meters.
internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly ConcurrentQueue<Meter> _meters = new();

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(new MeterOptions(options.Name) { Version = options.Version, Tags = options.Tags, Scope = this });
        _meters.Enqueue(meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }
    }
}

// A MeterListener whose callback throws (an InvalidOperationException unless told otherwise) on every ducky.persistence.*
// measurement of one factory's meters, after noting it.
internal sealed class ThrowingMeterListener : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly Exception? _exception;

    public ThrowingMeterListener(IMeterFactory factory, Exception? exception = null)
    {
        _exception = exception;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Scope == factory && instrument.Name.StartsWith("ducky.persistence.", StringComparison.Ordinal))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => Throw(instrument));
        _listener.SetMeasurementEventCallback<double>((instrument, _, _, _) => Throw(instrument));
        _listener.Start();
    }

    public ConcurrentQueue<string> Measured { get; } = new();

    public void Dispose() => _listener.Dispose();

    private void Throw(Instrument instrument)
    {
        Measured.Enqueue(instrument.Name);
        throw _exception ?? new InvalidOperationException("listener throws");
    }
}

// One storageSet call: what the writer stored, and its payload s.
internal sealed record StoredWrite(string Area, string Key, string Value, string Id)
{
    public string Payload => JsonDocument.Parse(Value).RootElement.GetProperty("s").GetRawText();
}

// One storageRemove call.
internal sealed record StoredRemoval(string Area, string Key, string Id);

// A FakeTimeProvider that reports each timer as it is created, so a test advances time only once the writer waits.
internal sealed class WatchedTime() : FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero))
{
    private readonly Channel<TimeSpan> _timers = Channel.CreateUnbounded<TimeSpan>();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        _timers.Writer.TryWrite(dueTime);
        return timer;
    }

    /// <summary>Waits until a timer of <paramref name="dueTime"/> has been created.</summary>
    public async Task TimerAsync(TimeSpan dueTime)
    {
        while (await _timers.Reader.ReadAsync(Xunit.TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10)) != dueTime)
        {
        }
    }
}

// One store over a FakeJsRuntime whose storage is a dictionary (§17.2): storageSet records each write and answers what
// OnSet decides (a value, a Task<object?> to hold or fail the call, or a throw).
internal sealed class WriterHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private readonly Channel<StoredWrite> _writes = Channel.CreateUnbounded<StoredWrite>();
    private readonly Channel<StoredRemoval> _removals = Channel.CreateUnbounded<StoredRemoval>();

    public WriterHarness(Action<DuckyBuilder> configure, bool browser = true, Action<DuckyBuilder>? early = null, Action<IServiceCollection>? services = null)
    {
        OnGet = Get;
        Js.Respond = (identifier, args) => identifier switch
        {
            "import" => Js,
            "storageGet" => OnGet(args),
            "storageSet" => Set(new((string)args[0]!, (string)args[1]!, (string)args[2]!, (string)args[3]!)),
            "storageRemove" => Remove(new((string)args[0]!, (string)args[1]!, (string)args[2]!)),
            _ => null,
        };

        var collection = new ServiceCollection();
        collection.AddSingleton<IJSRuntime>(Js).AddSingleton<TimeProvider>(Time).AddSingleton(Log).AddSingleton(Inbox).AddSingleton(Line);
        collection.AddSingleton<IMeterFactory>(Meters);
        services?.Invoke(collection);
        collection.AddDucky(d =>
        {
            d.UseJson(WriterJson.Default).AddSlice<LevelSlice>().AddSlice<DialSlice>().AddSlice<CounterSlice>();
            early?.Invoke(d);
            d.AddBlazor(o => o.IsBrowser = browser);
            configure(d);
            d.Use<HydrationRecorder>().Use<FailureTap>().Use<SystemSender>();
        });
        _provider = collection.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();
    }

    public FakeJsRuntime Js { get; } = new();

    public WatchedTime Time { get; } = new();

    /// <summary>The store's IMeterFactory: every Meter it creates is scoped to it.</summary>
    public TestMeterFactory Meters { get; } = new();

    public HydrationLog Log { get; } = new();

    public FailureInbox Inbox { get; } = new();

    public SystemLine Line { get; } = new();

    public ConcurrentDictionary<(string Area, string Key), string> Storage { get; } = new();

    /// <summary>What storageGet answers; by default the stored value.</summary>
    public Func<object?[], object?> OnGet { get; set; }

    /// <summary>What storageSet answers once the write is recorded; by default true (the id is registered).</summary>
    public Func<StoredWrite, object?> OnSet { get; set; } = static _ => true;

    public IStore Store => _scope.ServiceProvider.GetRequiredService<IStore>();

    public PersistenceMiddleware Middleware => _scope.ServiceProvider.GetRequiredService<PersistenceSlice>().Middleware.ShouldNotBeNull();

    /// <summary>What storageRemove answers once the key is removed from <see cref="Storage"/>; by default true.</summary>
    public Func<StoredRemoval, object?> OnRemove { get; set; } = static _ => true;

    public IReadOnlyList<StoredWrite> Written => [.. Js.Calls.Where(static call => call.Identifier == "storageSet").Select(static call => new StoredWrite((string)call.Args[0]!, (string)call.Args[1]!, (string)call.Args[2]!, (string)call.Args[3]!))];

    public static string Payload(object state) => JsonSerializer.Serialize(state, state.GetType(), WriterJson.Default);

    public Task InitializeAsync() => Store.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

    /// <summary>The next storageSet call, in call order.</summary>
    public Task<StoredWrite> NextWriteAsync() =>
        _writes.Reader.ReadAsync(Xunit.TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    /// <summary>The next storageRemove call, in call order.</summary>
    public Task<StoredRemoval> NextRemovalAsync() =>
        _removals.Reader.ReadAsync(Xunit.TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    /// <summary>Takes the storageSet calls already made, without waiting.</summary>
    public List<StoredWrite> TakeWrites()
    {
        List<StoredWrite> taken = [];
        while (_writes.Reader.TryRead(out var write))
        {
            taken.Add(write);
        }

        return taken;
    }

    /// <summary>Waits until at least one more storageSet call has been made.</summary>
    public Task WriteArrivedAsync() =>
        _writes.Reader.WaitToReadAsync(Xunit.TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    /// <summary>The next PersistenceFailed the store processed.</summary>
    public Task<PersistenceFailed> NextFailureAsync() =>
        Inbox.Failures.Reader.ReadAsync(Xunit.TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    /// <summary>Waits until the writer of <paramref name="key"/> has ended <paramref name="iterations"/> iterations.</summary>
    public Task IterationsAsync(string key, int iterations) => Until(() => Middleware.Writers[key].Iterations >= iterations);

    /// <summary>Yields until <paramref name="condition"/> holds, bounded by 10 s.</summary>
    public static async Task Until(Func<bool> condition)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Xunit.TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            bound.Token.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    /// <summary>Disposes the store itself (the scope's later dispose is a no-op for it).</summary>
    public Task DisposeStoreAsync() => Store.DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
        Meters.Dispose();
    }

    private object? Set(StoredWrite write)
    {
        Log.Add($"write:{write.Key}:{write.Payload}");

        // Announced once OnSet has run (or thrown), so a test that sees the write also sees what OnSet did with it.
        try
        {
            return OnSet(write);
        }
        finally
        {
            _writes.Writer.TryWrite(write);
        }
    }

    private object? Remove(StoredRemoval removal)
    {
        Log.Add($"remove:{removal.Key}");
        Storage.TryRemove((removal.Area, removal.Key), out _);
        try
        {
            return OnRemove(removal);
        }
        finally
        {
            _removals.Writer.TryWrite(removal);
        }
    }

    private string? Get(object?[] args) => Storage.GetValueOrDefault(((string)args[0]!, (string)args[1]!));
}

[JsonSerializable(typeof(Level))]
[JsonSerializable(typeof(Dial))]
[JsonSerializable(typeof(Counter))]
internal sealed partial class WriterJson : JsonSerializerContext;
