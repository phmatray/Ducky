namespace Plain;

/// <summary>A capacity that callers take from; every await shape of src/ (SPEC §17.8, S-6).</summary>
public sealed class Budget(int capacity)
{
    private int _used;

    /// <summary>What is left.</summary>
    public int Remaining => capacity - _used;

    /// <summary>Takes <paramref name="amount"/> when it fits.</summary>
    public bool TryTake(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (_used + amount > capacity)
        {
            return false;
        }

        _used += amount;
        return true;
    }

    /// <summary>Task await: ConfigureAwaitOptions.None satisfies CA2007 with no boolean literal to mutate.</summary>
    public async Task<bool> TryTakeWithOptionsAsync(Func<Task<int>> amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        var value = await amount().ConfigureAwait(ConfigureAwaitOptions.None);
        return TryTake(value);
    }

    /// <summary>ValueTask await: no ConfigureAwaitOptions overload.</summary>
    public async ValueTask<bool> TryTakeValueAsync(Func<ValueTask<int>> amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        // Stryker disable once Boolean : ConfigureAwait is unobservable without a SynchronizationContext
        var value = await amount().ConfigureAwait(false);
        return TryTake(value);
    }

    /// <summary>await using: no ConfigureAwaitOptions overload either.</summary>
    public async Task<int> ReleaseAsync(IAsyncDisposable lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        // Stryker disable once Boolean : ConfigureAwait is unobservable without a SynchronizationContext
        await using (lease.ConfigureAwait(false))
        {
            var released = _used;
            _used = 0;
            return released;
        }
    }
}
