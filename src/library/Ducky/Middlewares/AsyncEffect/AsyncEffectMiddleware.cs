using Ducky.Pipeline;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Middleware that executes asynchronous actions implementing <see cref="IAsyncEffect"/> when they are dispatched.
/// </summary>
/// <typeparam name="TState">The type of the application state.</typeparam>
public sealed class AsyncEffectMiddleware<TState> : StoreMiddleware
    where TState : class
{
    private IDispatcher? _dispatcher;
    private IStore? _store;

    /// <inheritdoc />
    public override Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override StoreMiddlewareAsyncMode AsyncMode
        => StoreMiddlewareAsyncMode.FireAndForget;

    /// <summary>
    /// Checks if the action implements <see cref="IAsyncEffect"/> and, if so, executes it asynchronously.
    /// </summary>
    public override Task BeforeDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        if (_store is not null && _dispatcher is not null)
        {
            // Check if the action implements IAsyncEffect
            if (context.Action is IAsyncEffect asyncEffect)
            {
                asyncEffect.SetDispatcher(_dispatcher);

                if (asyncEffect.CanHandle(context.Action))
                {
                    // Fire-and-forget the async effect. No need to block the pipeline.
                    asyncEffect.HandleAsync(context.Action, _store.CurrentState);
                }
            }
        }

        return Task.CompletedTask;
    }
}
