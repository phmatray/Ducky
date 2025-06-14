using Ducky.Pipeline;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Middleware that executes asynchronous effects implementing <see cref="IAsyncEffect"/> when they are dispatched.
/// </summary>
public sealed class AsyncEffectMiddleware : MiddlewareBase
{
    private readonly IEnumerable<IAsyncEffect> _effects;
    private readonly IStoreEventPublisher _eventPublisher;
    private IStore? _store;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncEffectMiddleware"/> with the specified dependencies.
    /// </summary>
    /// <param name="effects">An enumerable collection of asynchronous effects that implement <see cref="IAsyncEffect"/>.</param>
    /// <param name="eventPublisher">The store event publisher for error events.</param>
    public AsyncEffectMiddleware(
        IEnumerable<IAsyncEffect> effects,
        IStoreEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _effects = effects;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc />
    public override Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _store = store;

        // Inject the dispatcher into each effect
        foreach (IAsyncEffect effect in _effects)
        {
            effect.SetDispatcher(dispatcher);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override bool MayDispatchAction(object action)
    {
        // Allow dispatch if any effect can handle the action
        // TODO: Fix this logic to properly check if any effect can handle the action
        // return _effects.Any(effect => effect.CanHandle(action));
        return true;
    }

    /// <inheritdoc />
    public override void AfterReduce(object action)
    {
        TriggerEffects(action);
    }

    private void TriggerEffects(object action)
    {
        List<Exception> recordedExceptions = [];
        IAsyncEffect[] effectsToExecute = _effects
            .Where(effect => effect.CanHandle(action))
            .ToArray();
        List<Task> executedEffects = [];

        Action<Exception> collectExceptions = exception =>
        {
            if (exception is AggregateException aggregateException)
            {
                recordedExceptions.AddRange(aggregateException.Flatten().InnerExceptions);
            }
            else
            {
                recordedExceptions.Add(exception);
            }
        };

        // Execute all effects. Some will execute synchronously and complete immediately,
        // so we need to catch their exceptions in the loop so they don't prevent
        // other effects from executing.
        foreach (IAsyncEffect effect in effectsToExecute)
        {
            try
            {
                executedEffects.Add(effect.HandleAsync(action, _store!));
            }
            catch (Exception exception)
            {
                collectExceptions(exception);
            }
        }

        // Fire and forget - handle all async effects concurrently
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(executedEffects).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                collectExceptions(exception);
            }

            // Publish all collected exceptions as error events
            foreach (Exception exception in recordedExceptions)
            {
                Type effectType = effectsToExecute
                    .FirstOrDefault(_ => executedEffects.Any(t => t.Exception?.InnerException == exception))
                    ?.GetType()
                    ?? typeof(IAsyncEffect);

                EffectErrorEventArgs errorEventArgs = new(exception, effectType, action);
                _eventPublisher.Publish(errorEventArgs);
            }
        });
    }
}
