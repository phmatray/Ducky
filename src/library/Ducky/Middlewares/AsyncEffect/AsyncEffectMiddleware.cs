using Ducky.Pipeline.Reactive;
using Microsoft.Extensions.DependencyInjection;
using R3;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Middleware that executes asynchronous effects implementing <see cref="IAsyncEffect"/> when they are dispatched.
/// </summary>
public sealed class AsyncEffectMiddleware : IActionMiddleware
{
    private readonly IServiceProvider _services;
    private readonly Func<IRootState> _getState;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncEffectMiddleware"/> with the specified service provider and state getter.
    /// </summary>
    /// <param name="services">The service provider used to resolve dependencies.</param>
    /// <param name="getState">A function that returns the current state of the application.</param>
    public AsyncEffectMiddleware(IServiceProvider services, Func<IRootState> getState)
    {
        _services  = services;
        _getState  = getState;
    }

    /// <inheritdoc />
    public Observable<ActionContext> Invoke(Observable<ActionContext> actions)
    {
        // grab all registered IAsyncEffect<TState> from DI
        IAsyncEffect[] effects = _services.GetServices<IAsyncEffect>().ToArray();

        // side-effect: for each incoming context whose Action any effect can handle,
        // fire off the async handler (does not block the pipeline)
        return actions.Do(ctx =>
        {
            foreach (IAsyncEffect effect in effects)
            {
                if (effect.CanHandle(ctx.Action))
                {
                    _ = effect.HandleAsync(ctx.Action, _getState());
                }
            }
        });
    }
}

// /// <summary>
// /// Middleware that executes asynchronous actions implementing <see cref="IAsyncEffect"/> when they are dispatched.
// /// </summary>
// /// <typeparam name="TState">The type of the application state.</typeparam>
// public sealed class AsyncEffectMiddleware<TState> : StoreMiddleware
//     where TState : class
// {
//     /// <inheritdoc />
//     public override StoreMiddlewareAsyncMode AsyncMode
//         => StoreMiddlewareAsyncMode.FireAndForget;
//
//     /// <summary>
//     /// Checks if the action implements <see cref="IAsyncEffect"/> and, if so, executes it asynchronously.
//     /// </summary>
//     public override async Task BeforeDispatchAsync<TAction>(
//         ActionContext<TAction> context,
//         CancellationToken cancellationToken = default)
//     {
//         // Check if the action implements IAsyncEffect
//         if (context.Action is IAsyncEffect asyncEffect)
//         {
//             asyncEffect.SetDispatcher(Dispatcher);
//
//             if (asyncEffect.CanHandle(context.Action))
//             {
//                 // Fire-and-forget the async effect. No need to block the pipeline.
//                 _ = asyncEffect.HandleAsync(context.Action, Store.CurrentState);
//             }
//         }
//
//         await Task.CompletedTask.ConfigureAwait(false);
//     }
// }
