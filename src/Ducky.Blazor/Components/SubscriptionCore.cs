using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Blazor;

/// <summary>
/// The one implementation behind every Ducky component's selections (SPEC §11.2, D9, ADR-0028, INV-19). The drainer only
/// schedules: one store subscription flips <c>_scheduled</c> 0→1 and posts one <see cref="Evaluate"/>, which runs on the
/// renderer with the current parameter values and compares each selection with the value the renderer last observed.
/// </summary>
internal sealed class SubscriptionCore(IStore store, Func<Func<Task>, Task> invokeAsync, Action stateHasChanged, ILogger logger) : IDisposable
{
    // An empty ExecutionContext: ExecutionContext.Default, which is not public, is what Capture returns on a thread started
    // without flow. Captured once per process. Null where no thread can start (single-threaded WebAssembly, WASI): there
    // the renderer dispatches inline (NullDispatcher), so no posted check ever runs on another caller's thread.
#pragma warning disable CA1416 // justification: on browser UnsafeStart throws PlatformNotSupportedException, which CaptureEmptyContext catches
    internal static readonly ExecutionContext? EmptyContext = CaptureEmptyContext(static thread => thread.UnsafeStart());
#pragma warning restore CA1416

    private readonly SafeLogger _logger = new(logger);

    // Renderer-only: Select (registration), each check's baseline (render reads and Evaluate) and Dispose.
    private readonly List<Func<StateSnapshot, bool>> _checks = [];
    private Selection<StateSnapshot>? _subscription;
    private volatile bool _disposed;
    private int _scheduled;

    // The context the change check is scheduled under (§11.2): the empty one, so neither the drainer's nor the creator's
    // AsyncLocals (a causal scope, an effect run) reach it. Tests set null to take the fallback.
    internal ExecutionContext? Context { private get; init; } = EmptyContext;

    /// <summary>
    /// Creates a core that logs through the container's <see cref="ILogger{TCategoryName}"/>, when there is one. It first
    /// records the renderer and hands over <paramref name="services"/> to the store's gate (§11.2), before anything
    /// touches the store.
    /// </summary>
    public static SubscriptionCore Create(IStore store, IServiceProvider services, Func<RendererInfo> renderer, Func<Func<Task>, Task> invokeAsync, Action stateHasChanged)
    {
        InteractivityGate.HandOver(services, renderer);
        return new(store, invokeAsync, stateHasChanged, (ILogger?)services.GetService<ILogger<SubscriptionCore>>() ?? NullLogger.Instance);
    }

    /// <summary>
    /// Registers a selection. Its <see cref="Selection{T}.Value"/> evaluates on read, so a parameter change shows on the
    /// next render with no dispatch; each read is the renderer-observed baseline of the change check.
    /// </summary>
    public Selection<T> Select<T>(Func<StateSnapshot, T> selector, IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        _subscription ??= store.Select<StateSnapshot>(static s => s, _ => OnCommit(), ReferenceEqualityComparer.Instance);
        T observed = default!;
        Func<StateSnapshot, bool> changed = state =>
        {
            var value = selector(state);
            var differs = !comparer.Equals(observed, value);
            observed = value;
            return differs;
        };
        _checks.Add(changed);
        return Selection<T>.Create(() => selector(store.State), value => observed = value, () => _checks.Remove(changed));
    }

    /// <summary>Idempotent: disposes the store subscription; a pending <see cref="Evaluate"/> then renders nothing.</summary>
    public void Dispose()
    {
        _disposed = true;
        _subscription?.Dispose();
    }

    // Runs ExecutionContext.Capture on a new thread, started by start without flow (UnsafeStart), so the thread has no
    // context and Capture returns the empty one. Null when the platform can't start a thread.
    internal static ExecutionContext? CaptureEmptyContext(Action<Thread> start)
    {
        ExecutionContext? empty = null;
        var thread = new Thread(() => empty = ExecutionContext.Capture());
        try
        {
            start(thread);
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }

        thread.Join();
        return empty;
    }

    // Drainer. The full-fence CAS orders this commit's snapshot write before a pending Evaluate's reset, so that Evaluate
    // reads it: no lost wake-up, and at most one pending InvokeAsync. Scheduled under the empty context, everything the
    // check posts carries it, so it runs with no AsyncLocal wherever it runs, ExecutionContext.RunInternal restores the
    // thread it ran on, and ExecutionContext.Run gives the drainer back its own context and synchronization context.
    private void OnCommit()
    {
        if (Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0)
        {
            if (Context is null)
            {
                _ = ScheduleAsync();
            }
            else
            {
                ExecutionContext.Run(Context, static core => _ = ((SubscriptionCore)core!).ScheduleAsync(), this);
            }
        }
    }

    // When InvokeAsync runs the delegate inline (the drainer is on the renderer, or the renderer is idle), the Task.Yield
    // posts Evaluate behind the current work item. Evaluate never throws, so a failure here happened before it started:
    // only then is the flag reset.
    private async Task ScheduleAsync()
    {
        try
        {
            await StartEvaluationAsync().ConfigureAwait(ConfigureAwaitOptions.None);
        }
        catch (ObjectDisposedException)
        {
            // The renderer is torn down: nothing will render this component again.
        }
        catch (Exception ex)
        {
            Interlocked.CompareExchange(ref _scheduled, 0, 1);
            Log.ScheduleFailed(_logger, ex);
        }
    }

    // Under the empty context (OnCommit), the work InvokeAsync posts carries it. Without one, flow is suppressed here, which
    // keeps the drainer's context out of work that runs on a thread-pool thread; there is no renderer synchronization
    // context to leak on the platforms where that happens.
    private Task StartEvaluationAsync()
    {
        if (Context is not null)
        {
            return invokeAsync(EvaluateAsync);
        }

        using (ExecutionContext.SuppressFlow())
        {
            return invokeAsync(EvaluateAsync);
        }
    }

    private async Task EvaluateAsync()
    {
        await Task.Yield();
        Evaluate();
    }

    // Renderer. Resets the flag first, so a commit from here on schedules the next check.
    private void Evaluate()
    {
        Interlocked.Exchange(ref _scheduled, 0);
        try
        {
            if (!_disposed && Changed())
            {
                stateHasChanged();
            }
        }
        catch (Exception ex)
        {
            // Changed() catches every selector and comparer throw, so only stateHasChanged() reaches here.
            Log.RenderRequestFailed(_logger, ex);
        }
    }

    // Every check runs against one snapshot, so each baseline moves. A throwing selector or comparer counts as changed:
    // the render then reads it again and surfaces the exception through the component's error path.
    private bool Changed()
    {
        var state = store.State;
        var changed = false;
        foreach (var check in _checks)
        {
            try
            {
                changed |= check(state);
            }
            catch (Exception ex)
            {
                Log.EvaluateFailed(_logger, ex);
                changed = true;
            }
        }

        return changed;
    }
}
