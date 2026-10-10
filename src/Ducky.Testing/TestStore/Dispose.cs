namespace Ducky.Testing;

public sealed partial class TestStore
{
    /// <summary>Gets or sets the real-time bound of <see cref="Settled"/> and <see cref="DisposeAsync"/>; 10 seconds by default.</summary>
    public TimeSpan WallClockTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Disposes the store and its container.</summary>
    public void Dispose()
    {
        _scope.Dispose();
        _services.Dispose();
    }

    /// <summary>
    /// Disposes the store and its container within <see cref="WallClockTimeout"/> of real time: the store's own dispose
    /// bounds run on <see cref="Time"/>, which nobody advances during disposal (SPEC §15).
    /// </summary>
    /// <returns>A task that completes once the store is disposed.</returns>
    /// <exception cref="DuckyAssertionException">The store was not disposed within <see cref="WallClockTimeout"/> (on the task).</exception>
    public ValueTask DisposeAsync()
    {
        var bound = WallClockTimeout;
        return new(BoundAsync(
            DisposeContainersAsync(),
            bound,
            () => $"The store was not disposed after {bound} of real time: an effect run, middleware init or JS/cache call likely "
                + "ignores cancellation. Last processed actions: "
                + $"{string.Join(", ", Processed.TakeLast(5).Select(c => c.ActionType).DefaultIfEmpty("(none)"))}."));
    }

    private async Task DisposeContainersAsync()
    {
        // Stryker disable once Boolean : ConfigureAwait is unobservable without a SynchronizationContext
        await _scope.DisposeAsync().ConfigureAwait(false);

        // Stryker disable once Boolean : ConfigureAwait is unobservable without a SynchronizationContext
        await _services.DisposeAsync().ConfigureAwait(false);
    }
}
