using Ducky.Pipeline;
using R3;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Middleware that sends every dispatched action and resulting state to the Redux DevTools browser extension.
/// This middleware integrates with the Redux DevTools extension to provide time-travel debugging capabilities.
/// </summary>
public sealed class DevToolsMiddleware : IActionMiddleware
{
    private readonly ReduxDevToolsModule _devTools;
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevToolsMiddleware"/> class.
    /// </summary>
    /// <param name="devTools">The DevTools JSInterop module.</param>
    /// <param name="store">The Ducky store instance.</param>
    public DevToolsMiddleware(ReduxDevToolsModule devTools, IStore store)
    {
        _devTools = devTools ?? throw new ArgumentNullException(nameof(devTools));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        // DevTools doesn't need to process actions before reduction
        // Time-travel state restoration is handled via special actions
        return actions;
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        // Send action and state to DevTools after reduction
        return actions.Do(context =>
        {
            // Fire-and-forget to avoid blocking the pipeline
            _ = _devTools.SendAsync(context.Action, _store.CurrentState);
        });
    }
}
