using Microsoft.AspNetCore.Components;

namespace Ducky.Blazor;

/// <summary>Settling a rendered component in a test (#297).</summary>
public static class StoreRenderingExtensions
{
    /// <summary>
    /// Waits until the store is idle, then until the change checks that idle point scheduled have run on the renderer, so a
    /// test can assert on the markup of a component that selects from the store. In bUnit:
    /// <c>await store.WhenRenderedAsync(Renderer.Dispatcher)</c> after a dispatch or an interaction.
    /// </summary>
    /// <remarks>
    /// The render a change causes is posted by <c>SubscriptionCore</c> to the renderer once the drainer commits it, and
    /// takes two renderer turns. That scheduling stays here, next to it. The check runs under an empty execution context,
    /// so culture-sensitive output after a dispatch follows #296.
    /// </remarks>
    /// <param name="store">The store the component selects from.</param>
    /// <param name="dispatcher">The renderer's dispatcher (bUnit: <c>Renderer.Dispatcher</c>).</param>
    /// <param name="cancellationToken">Cancels the wait for the store.</param>
    /// <returns>A task that completes when the component has rendered what the store committed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="dispatcher"/> is null.</exception>
    public static async Task WhenRenderedAsync(this IStore store, Dispatcher dispatcher, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dispatcher);
        await store.WhenIdleAsync(cancellationToken).ConfigureAwait(false);

        // SubscriptionCore.ScheduleAsync posts EvaluateAsync (turn 1), which Task.Yields before Evaluate (turn 2): keep the
        // two in step. A change to that scheduling and to this method land in the same PR.
        await dispatcher.InvokeAsync(static () => { }).ConfigureAwait(false);
        await dispatcher.InvokeAsync(static () => { }).ConfigureAwait(false);
    }
}
