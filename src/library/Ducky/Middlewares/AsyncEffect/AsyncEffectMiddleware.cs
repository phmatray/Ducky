using Ducky.Pipeline;
using R3;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Middleware that executes asynchronous effects implementing <see cref="IAsyncEffect"/> when they are dispatched.
/// </summary>
public sealed class AsyncEffectMiddleware : IActionMiddleware
{
    private readonly IEnumerable<IAsyncEffect> _effects;
    private readonly Func<IRootState> _getState;
    private readonly IStoreEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncEffectMiddleware"/> with the specified dependencies.
    /// </summary>
    /// <param name="effects">An enumerable collection of asynchronous effects that implement <see cref="IAsyncEffect"/>.</param>
    /// <param name="getState">A function that returns the current state of the application.</param>
    /// <param name="dispatcher">The dispatcher used to handle actions.</param>
    /// <param name="eventPublisher">The store event publisher for error events.</param>
    public AsyncEffectMiddleware(
        IEnumerable<IAsyncEffect> effects,
        Func<IRootState> getState,
        IDispatcher dispatcher,
        IStoreEventPublisher eventPublisher)
    {
        _effects = effects;
        _getState = getState;
        _eventPublisher = eventPublisher;

        // Inject the dispatcher into each effect
        foreach (IAsyncEffect effect in _effects)
        {
            effect.SetDispatcher(dispatcher);
        }
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        return actions;
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        // side-effect: for each incoming context whose Action any effect can handle,
        // fire off the async handler (does not block the pipeline)
        return actions.Do(ctx =>
        {
            foreach (IAsyncEffect effect in _effects)
            {
                if (effect.CanHandle(ctx.Action))
                {
                    // Fire and forget with exception handling
                    _ = HandleEffectAsync(effect, ctx.Action);
                }
            }
        });
    }

    private async Task HandleEffectAsync(IAsyncEffect effect, object action)
    {
        try
        {
            await effect.HandleAsync(action, _getState()).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Publish error event for async effects
            EffectErrorEventArgs errorEventArgs = new(exception, effect.GetType(), action);
            _eventPublisher.Publish(errorEventArgs);
        }
    }
}
