using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using R3;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Middleware that executes asynchronous effects implementing <see cref="IAsyncEffect"/> when they are dispatched.
/// </summary>
public sealed class AsyncEffectMiddleware : IActionMiddleware
{
    private readonly Func<IRootState> _getState;
    private readonly IAsyncEffect[] _effects;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncEffectMiddleware"/> with the specified dependencies.
    /// </summary>
    /// <param name="services">The service provider used to resolve dependencies.</param>
    /// <param name="getState">A function that returns the current state of the application.</param>
    /// <param name="dispatcher">The dispatcher used to handle actions.</param>
    public AsyncEffectMiddleware(
        IServiceProvider services,
        Func<IRootState> getState,
        IDispatcher dispatcher)
    {
        _getState = getState;

        // Resolve and cache effects
        _effects = services.GetServices<IAsyncEffect>().ToArray();

        // Optionally, inject the dispatcher into each effect if needed:
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
                    _ = effect.HandleAsync(ctx.Action, _getState());
                }
            }
        });
    }
}
