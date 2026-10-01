namespace Ducky;

// One IStore.Select with an onChange (§6.8). `last` starts Unset (null) and is installed by a CAS from Unset, either by
// the registering thread after its State read or by the drainer, whichever comes first; after that only the drainer
// writes it, so a commit racing the registration is never lost.
internal abstract class Subscription
{
    // Process step 10, on the drainer.
    internal abstract void Notify(StateSnapshot state);
}

internal sealed class Subscription<T>(Func<StateSnapshot, T> selector, Action<T> onChange, IEqualityComparer<T> comparer)
    : Subscription
{
    private Last? _last;

    // Select step 3, on the registering thread: a no-op when the drainer installed first.
    internal void Install(StateSnapshot state) => Interlocked.CompareExchange(ref _last, new(selector(state)), null);

    internal override void Notify(StateSnapshot state)
    {
        var next = selector(state);
        var last = Interlocked.CompareExchange(ref _last, new(next), null);
        if (last is null || comparer.Equals(last.Value, next))
        {
            return;
        }

        // M3-01b: a throw must leave `last` unchanged (§6.8), so with the per-subscription try/catch this write moves
        // after onChange; that story also adds the _disposed check before the selector.
        Volatile.Write(ref _last, new(next));
        onChange(next);
    }

    // A reference holder, so `last` can be CASed from Unset whatever T is.
    private sealed record Last(T Value);
}
