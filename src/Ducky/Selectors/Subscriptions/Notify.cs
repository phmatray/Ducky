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

    // Idempotent: removing an absent subscription changes nothing.
    internal void Unsubscribe(Subscription subscription) =>
        ImmutableInterlocked.Update(ref _subscribers, static (list, s) => list.Remove(s), subscription);

    // §6.11 step 6: releases every subscription and, through it, its onChange closure.
    private void ClearSubscribers() => ImmutableInterlocked.InterlockedExchange(ref _subscribers, []);

    // Step 10, only after a commit that changed the snapshot. No lock is held (INV-05, INV-09).
    private void Notify()
    {
        // Pairs with Subscribe's CAS: the commit's write and this read must not reorder (store-load), or a Select that
        // read the old snapshot after its add could be missed here too.
        // Stryker disable once Statement : a full fence; only a real interleaving (M3-04) can observe its absence
        Interlocked.MemoryBarrier();
        var state = State;
        foreach (var subscription in _subscribers)
        {
            subscription.Notify(state);
        }
    }
}
