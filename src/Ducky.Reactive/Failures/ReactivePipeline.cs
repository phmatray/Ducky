using R3;

namespace Ducky.Reactive;

// One reactive effect's subscription (SPEC §14, INV-27): emissions are dispatched, every failure (a throwing Handle, a
// non-terminal error, a failure completion) is logged and published as a System failure action, and a failure that ends
// the pipeline resubscribes after RetryDelay(attempt) unless the store is disposed first. Rx's grammar serializes one
// subscription's callbacks, and a resubscribe starts only after the previous one ended, so _attempt needs no lock.
internal sealed class ReactivePipeline(
    ReactiveEffect effect,
    ReactiveBridge bridge,
    EffectContext context,
    CompositeDisposable subscriptions,
    SafeLogger logger,
    CancellationToken lifetime)
{
    private int _attempt;
    private IDisposable _current = Disposable.Empty;

    // A subscription assigned after the lifetime callback disposed the composite (and so its slot) is disposed at once. The
    // slot is recorded before any user code runs, so a resubscribe always finds it: it starts only after this one failed,
    // perhaps on another thread while this call is still on the stack. The previous slot leaves the composite then, since
    // an emission resets _attempt and a pipeline that emits and fails retries forever.
    // A slot already disposed means the lifetime callback disposed the composite first: a retry that passed the re-check
    // just before the store was disposed returns without calling Handle. One past the check holds the bridge's gate until
    // its subscription is made, and phase 5b takes the gate before it disposes the effects, so Handle never runs on a
    // disposed effect (§14, INV-27); the subscription lands in a disposed slot, which disposes it at once.
    public void Subscribe()
    {
        var slot = new SingleAssignmentDisposable();
        subscriptions.Add(slot);
        subscriptions.Remove(_current);
        _current = slot;
        lock (bridge.Gate)
        {
            if (slot.IsDisposed)
            {
                return;
            }

            try
            {
                slot.Disposable = new IsolatedSubscription(effect.Handle(bridge.Actions, State).Subscribe(OnNext, Publish, OnCompleted), effect.GetType(), logger);
                Log.EffectSubscribed(logger, effect.GetType());
            }
#pragma warning disable CA1031 // justification: Handle is user code; its throw is a reactive failure like any other (INV-27)
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Fail(ex);
            }
        }
    }

    // What a pipeline reads as state. Each subscription's first value is the store's live snapshot, read while the property
    // replays under its lock, so no write can slip between the read and the subscription: a subscription made between a
    // commit and the bridge's write (a resubscribe or an inner subscription from a timer thread, or from a user AfterReduce
    // that runs before ReactiveMiddleware) starts at the committed snapshot, never the bridge's previous one, and the
    // write that follows is the same instance, so it is not delivered twice (§14, INV-27).
    private Observable<StateSnapshot> State =>
        bridge.State.Select((replayed, index) => index == 0 ? context.State : replayed).DistinctUntilChanged(ReferenceEqualityComparer.Instance);

    private void OnNext(object action)
    {
        _attempt = 0;
        context.Dispatch(action);
    }

    private void OnCompleted(Result result)
    {
        if (result.IsFailure)
        {
            // The ! keeps the negated mutant compiling: under warnings-as-errors, a nullable warning would fail its build.
            Fail(result.Exception!);
        }
    }

    private void Publish(Exception exception)
    {
        Log.EffectFailed(logger, exception, effect.GetType());
        bridge.Middleware.PublishFailure(effect.GetType(), exception);
    }

    private void Fail(Exception exception)
    {
        Publish(exception);
        _attempt++;
        TimeSpan? delay = null;
        try
        {
            delay = effect.RetryDelay(_attempt);
        }
#pragma warning disable CA1031 // justification: RetryDelay is user code; its throw stops resubscribing, never the host (INV-27)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Publish(ex);
        }

        if (delay is null)
        {
            Log.ResubscribeStopped(logger, effect.GetType(), _attempt);
            return;
        }

        // At least one millisecond: Task.Delay completes a shorter delay synchronously, so a pipeline that fails on
        // subscribe would resubscribe on the stack of its own failure, again and again, until the stack overflows.
        _ = ResubscribeAsync(TimeSpan.FromTicks(Math.Max(delay.GetValueOrDefault().Ticks, TimeSpan.TicksPerMillisecond)));
    }

    // Never faults: Subscribe isolates Handle, both cancellations (the delay's own and the re-check) land in the one
    // cancellation catch, and a delay Task.Delay rejects (above about 49.7 days) is a failure that stops resubscribing. The
    // re-check is branch-free: under FakeTimeProvider the delay's completion resumes inline inside Advance, so no
    // deterministic test can cancel between them (§14, §17.1).
    private async Task ResubscribeAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, context.Time, lifetime).ConfigureAwait(ConfigureAwaitOptions.None);
            // Stryker disable once Statement : equivalent in deterministic tests, the delay completes inline inside Advance (§14)
            lifetime.ThrowIfCancellationRequested();
            Subscribe();
        }
        catch (OperationCanceledException)
        {
            // The store was disposed while waiting: a pending resubscribe never fires (INV-27).
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Publish(ex);
            Log.ResubscribeStopped(logger, effect.GetType(), _attempt);
        }
    }
}
