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
    private readonly SafeLogger _logger = new(logger);

    // Renderer-only: Select (registration), each check's baseline (render reads and Evaluate) and Dispose.
    private readonly List<Func<StateSnapshot, bool>> _checks = [];
    private Selection<StateSnapshot>? _subscription;
    private volatile bool _disposed;
    private int _scheduled;

    /// <summary>Creates a core that logs through the container's <see cref="ILogger{TCategoryName}"/>, when there is one.</summary>
    public static SubscriptionCore Create(IStore store, IServiceProvider services, Func<Func<Task>, Task> invokeAsync, Action stateHasChanged) =>
        new(store, invokeAsync, stateHasChanged, (ILogger?)services.GetService<ILogger<SubscriptionCore>>() ?? NullLogger.Instance);

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

    // Drainer. The full-fence CAS orders this commit's snapshot write before a pending Evaluate's reset, so that Evaluate
    // reads it: no lost wake-up, and at most one pending InvokeAsync.
    private void OnCommit()
    {
        if (Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0)
        {
            _ = ScheduleAsync();
        }
    }

    // SuppressFlow keeps the drainer's ExecutionContext (its causal scope) out of posted work; when InvokeAsync runs the
    // delegate inline (the drainer is on the renderer), the Task.Yield inside the suppressed region posts Evaluate with an
    // empty one. Evaluate never throws, so a failure here happened before it started: only then is the flag reset.
    private async Task ScheduleAsync()
    {
        try
        {
            Task evaluation;
            using (ExecutionContext.SuppressFlow())
            {
                evaluation = invokeAsync(EvaluateAsync);
            }

            await evaluation.ConfigureAwait(ConfigureAwaitOptions.None);
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
