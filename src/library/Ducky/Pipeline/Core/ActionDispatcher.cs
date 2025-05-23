using Ducky.Middlewares;
using Ducky.Pipeline.Reactive;

namespace Ducky.Pipeline;

/// <summary>
/// Default implementation of <see cref="IActionDispatcher"/>.
/// </summary>
public sealed class ActionDispatcher : IActionDispatcher
{
    private readonly IPipelineEventPublisher _events;

    /// <summary>
    /// Initializes a new instance of <see cref="ActionDispatcher"/>.
    /// </summary>
    /// <param name="events">The pipeline event publisher.</param>
    public ActionDispatcher(IPipelineEventPublisher events)
    {
        _events = events;
    }

    /// <inheritdoc />
    public async Task DispatchAsync(
        ActionContext context,
        List<IStoreMiddleware> middlewares,
        CancellationToken cancellationToken = default)
    {
        _events.Publish(new ActionStartedEventArgs(context));
        await DispatchInternalAsync((dynamic)context, middlewares, cancellationToken);
        _events.Publish(new ActionCompletedEventArgs(context));
    }

    /// <summary>
    /// Internal implementation of action dispatching with middleware.
    /// </summary>
    private async Task DispatchInternalAsync<TAction>(
        ActionContext context,
        List<IStoreMiddleware> middlewares,
        CancellationToken cancellationToken = default)
    {
        // Before
        foreach (IStoreMiddleware middleware in middlewares)
        {
            if (middleware.CanHandle(typeof(TAction)))
            {
                try
                {
                    _events.Publish(new MiddlewareStartedEvent(context, middleware, StoreMiddlewarePhase.Before));
                    if (middleware.AsyncMode == StoreMiddlewareAsyncMode.Await)
                    {
                        await middleware.BeforeDispatchAsync(context, cancellationToken).ConfigureAwait(false);
                    }
                    else // Fire-and-forget
                    {
                        _ = Task.Run(
                            () => middleware
                                .BeforeDispatchAsync(context, cancellationToken)
                                .ContinueWith(
                                    t =>
                                    {
                                        if (t.Exception is null)
                                        {
                                            return;
                                        }

                                        MiddlewareErroredEventArgs eventArgs = new(
                                            context, middleware, StoreMiddlewarePhase.Before, t.Exception);

                                        _events.Publish(eventArgs);
                                    },
                                    TaskContinuationOptions.OnlyOnFaulted),
                            cancellationToken);
                    }

                    _events.Publish(new MiddlewareCompletedEventArgs(context, middleware, StoreMiddlewarePhase.Before));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _events.Publish(new MiddlewareErroredEventArgs(context, middleware, StoreMiddlewarePhase.Before, ex));
                    context.Abort();
                    _events.Publish(new ActionAbortedEventArgs(context, $"Exception: {ex.Message}"));
                    break;
                }

                if (cancellationToken.IsCancellationRequested || context.IsAborted)
                {
                    return;
                }
            }
        }

        if (cancellationToken.IsCancellationRequested || context.IsAborted)
        {
            return;
        }

        // Fire event for processing (optional)
        // _events.Publish(new ...);

        // After
        for (int i = middlewares.Count - 1; i >= 0; i--)
        {
            IStoreMiddleware middleware = middlewares[i];
            if (middleware.CanHandle(typeof(TAction)))
            {
                try
                {
                    _events.Publish(new MiddlewareStartedEvent(context, middleware, StoreMiddlewarePhase.After));
                    if (middleware.AsyncMode == StoreMiddlewareAsyncMode.Await)
                    {
                        await middleware.AfterDispatchAsync(context, cancellationToken).ConfigureAwait(false);
                    }
                    else // Fire-and-forget
                    {
                        _ = Task.Run(
                            () => middleware.AfterDispatchAsync(context, cancellationToken)
                                .ContinueWith(
                                    t =>
                                    {
                                        if (t.Exception is null)
                                        {
                                            return;
                                        }

                                        MiddlewareErroredEventArgs eventArgs = new(
                                            context, middleware, StoreMiddlewarePhase.After, t.Exception);

                                        _events.Publish(eventArgs);
                                    },
                                    TaskContinuationOptions.OnlyOnFaulted),
                            cancellationToken);
                    }

                    _events.Publish(new MiddlewareCompletedEventArgs(context, middleware, StoreMiddlewarePhase.After));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _events.Publish(new MiddlewareErroredEventArgs(context, middleware, StoreMiddlewarePhase.After, ex));
                    context.Abort();
                    _events.Publish(new ActionAbortedEventArgs(context, $"Exception: {ex.Message}"));
                    break;
                }

                if (cancellationToken.IsCancellationRequested || context.IsAborted)
                {
                    return;
                }
            }
        }
    }
}
