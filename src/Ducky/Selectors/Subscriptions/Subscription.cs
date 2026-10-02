namespace Ducky;

// One IStore.Select with an onChange (§6.8). `last` starts Unset (null) and is installed by a CAS from Unset, either by
// the registering thread after its State read or by the drainer, whichever comes first; after that only the drainer
// writes it, so a commit racing the registration is never lost.
internal abstract class Subscription
{
    // Set by Selection.Dispose before the removal and read by the drainer right before the selector, so a subscription
    // disposed after step 10 captured the array is skipped. A callback already running may still finish (§6.8).
    private volatile bool _disposed;

    internal bool Disposed => _disposed;

    internal void Dispose() => _disposed = true;

    // Process step 10, on the drainer: the selector, the comparer and onChange, in one try/catch held by the caller.
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

        // After onChange, so a throw leaves `last` unchanged (§6.8, INV-09) and the next change notifies again.
        onChange(next);
        Volatile.Write(ref _last, new(next));
    }

    // A reference holder, so `last` can be CASed from Unset whatever T is.
    private sealed record Last(T Value);
}
