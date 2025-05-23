using Ducky.Middlewares;
using Ducky.Pipeline;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Middleware that sends every dispatched action and resulting state to the Redux DevTools browser extension.
/// </summary>
/// <typeparam name="TState">The type of your application state.</typeparam>
public sealed class DevToolsMiddleware<TState> : StoreMiddleware
    where TState : class
{
    private readonly ReduxDevToolsModule<TState> _devTools;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevToolsMiddleware{TState}"/> class.
    /// </summary>
    /// <param name="devTools">The DevTools JSInterop module.</param>
    public DevToolsMiddleware(ReduxDevToolsModule<TState> devTools)
    {
        _devTools = devTools ?? throw new ArgumentNullException(nameof(devTools));
    }

    /// <summary>
    /// Always run fire-and-forget so pipeline is not blocked.
    /// </summary>
    public override StoreMiddlewareAsyncMode AsyncMode
        => StoreMiddlewareAsyncMode.FireAndForget;

    /// <inheritdoc />
    public override async Task AfterDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        IRootState state = Store.CurrentState;

        // Fire-and-forget DevTools sync
        _ = _devTools.SendAsync(context.Action!, state);

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
