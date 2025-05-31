using Ducky.Pipeline;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Middleware that executes asynchronous effects implementing <see cref="IAsyncEffect"/> when they are dispatched.
/// </summary>
public sealed class AsyncEffectMiddleware : IMiddleware
{
    private readonly IEnumerable<IAsyncEffect> _effects;
    private readonly IStoreEventPublisher _eventPublisher;
    private IDispatcher? _dispatcher;
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
    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _dispatcher = dispatcher;
        _store = store;

        // Inject the dispatcher into each effect
        foreach (IAsyncEffect effect in _effects)
        {
            effect.SetDispatcher(dispatcher);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AfterInitializeAllMiddlewares()
    {
        // Nothing to do after all middlewares are initialized
    }

    /// <inheritdoc />
    public bool MayDispatchAction(object action)
    {
        // Allow all actions
        return true;
    }

    /// <inheritdoc />
    public void BeforeDispatch(object action)
    {
        // Nothing to do before dispatch
    }

    /// <inheritdoc />
    public void AfterDispatch(object action)
    {
        TriggerEffects(action);
    }

    /// <inheritdoc />
    public IDisposable BeginInternalMiddlewareChange()
    {
        // Return a no-op disposable
        return new DisposableCallback(() => { });
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
                executedEffects.Add(effect.HandleAsync(action, _store?.CurrentState!));
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
