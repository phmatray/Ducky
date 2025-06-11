using Ducky.Pipeline;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Middleware that sends every dispatched action and resulting state to the Redux DevTools browser extension.
/// This middleware integrates with the Redux DevTools extension to provide time-travel debugging capabilities.
/// </summary>
public sealed class DevToolsMiddleware : MiddlewareBase
{
    private readonly ReduxDevToolsModule _devTools;
    private IStore? _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevToolsMiddleware"/> class.
    /// </summary>
    /// <param name="devTools">The DevTools JSInterop module.</param>
    public DevToolsMiddleware(ReduxDevToolsModule devTools)
    {
        _devTools = devTools ?? throw new ArgumentNullException(nameof(devTools));
    }

    /// <inheritdoc />
    public override Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _store = store;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void AfterReduce(object action)
    {
        if (_store is null)
        {
            return;
        }

        // Send action and state to DevTools after reduction
        // Fire-and-forget to avoid blocking the pipeline
        _ = _devTools.SendAsync(action, _store.CurrentState);
    }
}
