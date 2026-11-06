using System.Collections.Concurrent;
using System.Security.Claims;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.CrossTab;

internal sealed record Note(string Text);

internal sealed class NoteSlice : Slice<Note>
{
    protected override Note Initial => new("");
}

// The app's load on StoreInitialized (§11.7): it loads only what storage did not restore, and its result is a local change.
internal sealed class LoadUnlessRestored : Effect<StoreInitialized>
{
    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.State.WasRestored<Level>())
        {
            context.Dispatch(new SetLevel(99));
        }

        return Task.CompletedTask;
    }
}

// Every restore the store processed, in order: its origin and the app slices it changed (the persistence status aside).
internal sealed class RestoreLog
{
    public ConcurrentQueue<(Origin Origin, string Keys)> Entries { get; } = new();

    /// <summary>The StateRestored of every HydrationCompleted the store processed.</summary>
    public ConcurrentQueue<bool> Completed { get; } = new();

    /// <summary>Invoked on the drainer with each entry's keys, once persistence's AfterReduce has run on it.</summary>
    public Action<string>? OnEntry { get; set; }
}

internal sealed class RestoreTap(RestoreLog log) : Middleware
{
    public override void AfterReduce(ActionContext context)
    {
        if (context.Action is HydrationCompleted completed)
        {
            log.Completed.Enqueue(completed.StateRestored);
        }

        var keys = context.ChangedKeys.Where(static key => !key.StartsWith("@ducky/", StringComparison.Ordinal)).ToList();
        if (context.Origin is Origin.CrossTab or Origin.Hydration && keys.Count > 0)
        {
            log.Entries.Enqueue((context.Origin, string.Join(",", keys)));
            log.OnEntry?.Invoke(string.Join(",", keys));
        }
    }
}

// Once armed, fails the next CrossTab restore: its BeforeReduce throws, so the restore commits nothing (§6.4).
internal sealed class CrossTabFailure
{
    public bool Armed { get; set; }
}

internal sealed class FailCrossTab(CrossTabFailure failure) : Middleware
{
    public override void BeforeReduce(ActionContext context)
    {
        if (context.Origin == Origin.CrossTab && failure.Armed)
        {
            failure.Armed = false;
            throw new InvalidOperationException("restore refused");
        }
    }
}

internal sealed record Gauge(double Value);

// Only its initial state can't be serialized (a NaN double, §10).
internal sealed class GaugeSlice : Slice<Gauge>
{
    public GaugeSlice() => On<SetLevel>(static (_, action) => new(action.Value));

    protected override Gauge Initial => new(double.NaN);
}

// Runs its plan on the drainer, after an Increment was reduced: what it issues to the store queues behind this drain.
internal sealed class DrainerPlan
{
    public Action Run { get; set; } = static () => { };
}

internal sealed class RunOnDrainer(DrainerPlan plan) : Middleware
{
    public override void AfterReduce(ActionContext context)
    {
        if (context.Action is Increment)
        {
            plan.Run();
        }
    }
}

// A WriterHarness whose store receives storage notifications through the reference it registered with watchStorage, as
// ducky.js delivers them (§11.7), with its logs collected.
internal sealed class CrossTabHarness : IAsyncDisposable
{
    internal static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(50);

    public CrossTabHarness(Action<DuckyBuilder> configure, bool browser = true, Action<IServiceCollection>? services = null) =>
        H = new(d =>
            {
                d.AddSlice<NoteSlice>();
                configure(d);
                d.Use<RestoreTap>();
            },
            browser,
            services: s =>
            {
                s.AddSingleton(Restores).AddLogging(logging => logging.AddProvider(new FakeLoggerProvider(Logs)).SetMinimumLevel(LogLevel.Debug));
                services?.Invoke(s);
            });

    public WriterHarness H { get; }

    public RestoreLog Restores { get; } = new();

    public FakeLogCollector Logs { get; } = new();

    public IReadOnlyList<object?[]> Watches => [.. H.Js.Calls.Where(static call => call.Identifier == "watchStorage").Select(static call => call.Args)];

    public PersistenceMiddleware.CrossTabSync Sync => ((DotNetObjectReference<PersistenceMiddleware.CrossTabSync>)Watches.ShouldHaveSingleItem()[1]!).Value;

    public string Envelope(object state, int version = 1) => EnvelopeWriter.Write(WriterHarness.Payload(state), version, H.Time.GetUtcNow());

    /// <summary>
    /// Hands one notification to the store as ducky.js does, once storage holds what it names: a value or a removal of a
    /// key is stored first (the store re-reads the key when its debounce ends, §11.7), a too-large or keyless one not.
    /// </summary>
    public Task Notify(string source, string area, string? key, string? value, bool tooLarge = false)
    {
        if (key is not null && !tooLarge)
        {
            if (value is null)
            {
                H.Storage.TryRemove((area, key), out _);
            }
            else
            {
                H.Storage[(area, key)] = value;
            }
        }

        return Sync.OnStorageChangedAsync(source, area, key, value, tooLarge);
    }

    /// <summary>Holds the next storage read (the hydration read) on <paramref name="read"/>; later reads answer as before.</summary>
    public void HoldNextRead(TaskCompletionSource<object?> read)
    {
        var answer = H.OnGet;
        var held = 0;
        H.OnGet = args => Interlocked.Exchange(ref held, 1) == 0 ? read.Task : answer(args);
    }

    /// <summary>Delivers one notification, lets its 50 ms debounce elapse and waits until it was handled (a restore drains inline).</summary>
    public async Task ReceiveAsync(string source, string area, string? key, string? value, bool tooLarge = false)
    {
        var handled = Notify(source, area, key, value, tooLarge);
        H.Time.Advance(Debounce);
        await handled.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
    }

    public IEnumerable<int> EventIds(LogLevel level) => Logs.GetSnapshot().Where(record => record.Level == level).Select(static record => record.Id.Id);

    public ValueTask DisposeAsync() => H.DisposeAsync();
}

// The signed-in user once the test releases it: holds the Server-storage scope resolution (Scope ?? NameIdentifier, §11.6).
internal sealed class HeldAuthenticationStateProvider : AuthenticationStateProvider
{
    public TaskCompletionSource<string> User { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async Task<AuthenticationState> GetAuthenticationStateAsync() =>
        new(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, await User.Task)], "test")));
}
