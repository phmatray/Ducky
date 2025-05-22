using Ducky.Middlewares;

namespace Ducky.Pipeline;

/// <summary>
/// Default implementation of <see cref="IReduxMiddlewarePipeline"/>.
/// </summary>
public sealed class StoreMiddlewarePipeline : IReduxMiddlewarePipeline
{
    private readonly List<IStoreMiddleware> _middlewares = [];
    private readonly Queue<IActionContext> _queue = [];
    private bool _processing;
    private readonly IActionDispatcher _dispatcher;
    private readonly IPipelineEventPublisher _events;

    /// <summary>
    /// Initializes a new instance of <see cref="StoreMiddlewarePipeline"/>.
    /// </summary>
    /// <param name="dispatcher">The action dispatcher.</param>
    /// <param name="eventPublisher">The pipeline event publisher.</param>
    /// <param name="middlewares">The registered middleware.</param>
    public StoreMiddlewarePipeline(
        IActionDispatcher dispatcher,
        IPipelineEventPublisher eventPublisher,
        IEnumerable<IStoreMiddleware> middlewares)
    {
        _dispatcher = dispatcher;
        _events = eventPublisher;
        _middlewares.AddRange(middlewares);
    }

    /// <inheritdoc />
    public async Task ProcessAsync<TAction>(TAction action, CancellationToken cancellationToken = default)
    {
        ActionContext<TAction> context = new(action);
        _queue.Enqueue(context);

        // If not currently processing, process everything (including new enqueues)
        if (_processing)
        {
            return;
        }

        _processing = true;
        try
        {
            while (_queue.Count > 0)
            {
                IActionContext next = _queue.Dequeue();
                await _dispatcher.DispatchAsync(next, _middlewares, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _processing = false;
        }
    }
}
