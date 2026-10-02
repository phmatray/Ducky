using System.Collections.Immutable;

namespace Ducky;

// SubscriberList and Process step 10 (§6.4, §6.8): an immutable array swapped with ImmutableInterlocked, so subscribing
// or unsubscribing never takes a lock and notification iterates the array it captured.
internal sealed partial class Dispatcher
{
    private ImmutableArray<Subscription> _subscribers = [];

    // For tests: whether any subscription is held (§6.11 step 6 releases them all).
    internal bool HasSubscribers => !_subscribers.IsEmpty;

    // After disposal published _disposal, an add that lands after step 6's clear removes itself: both are interlocked,
    // so either the clear sees the add or this read sees _disposal.
    internal void Subscribe(Subscription subscription)
    {
        ImmutableInterlocked.Update(ref _subscribers, static (list, s) => list.Add(s), subscription);
        if (Volatile.Read(ref _disposal) is not null)
        {
            Unsubscribe(subscription);
        }
    }

    // Idempotent: removing an absent subscription changes nothing. The flag goes first, so a drain that already captured
    // the array skips it (§6.8).
    internal void Unsubscribe(Subscription subscription)
    {
        subscription.Dispose();
        ImmutableInterlocked.Update(ref _subscribers, static (list, s) => list.Remove(s), subscription);
    }

    // §6.11 step 6: releases every subscription and, through it, its onChange closure.
    private void ClearSubscribers() => ImmutableInterlocked.InterlockedExchange(ref _subscribers, []);

    // Step 10's interleaving seam (§6.8, §17.1): invoked between the capture and the iteration; null in production.
    internal Action? BeforeNotifyHook { get; set; }

    // Step 10, only after a commit that changed the snapshot. No lock is held (INV-05, INV-09). Each subscription runs in
    // its own try/catch: a throw is logged, its `last` stays and it stays subscribed, and the others and step 11 still run.
    private void Notify(Type actionType)
    {
        // Pairs with Subscribe's CAS: the commit's write and this read must not reorder (store-load), or a Select that
        // read the old snapshot after its add could be missed here too.
        // Stryker disable once Statement : a full fence; only a real interleaving (M3-04) can observe its absence
        Interlocked.MemoryBarrier();
        var state = State;
        var subscribers = _subscribers;
        BeforeNotifyHook?.Invoke();
        foreach (var subscription in subscribers)
        {
            if (subscription.Disposed)
            {
                continue;
            }

            try
            {
                subscription.Notify(state);
            }
#pragma warning disable CA1031 // justification: a subscriber is user code; its throw is logged, never the drain's (§6.4)
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Log.SubscriberThrew(logger, ex, actionType);
            }
        }
    }
}
