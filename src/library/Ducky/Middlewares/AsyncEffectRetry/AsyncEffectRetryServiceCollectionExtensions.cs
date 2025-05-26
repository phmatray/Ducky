// using Microsoft.Extensions.DependencyInjection;
//
// namespace Ducky.Middlewares.AsyncEffectRetry;
//
// /// <summary>
// /// Provides extension methods for registering the AsyncEffectRetry middleware and related services.
// /// </summary>
// public static class AsyncEffectRetryServiceCollectionExtensions
// {
//     /// <summary>
//     /// Registers the AsyncEffectRetry middleware for the given state type.
//     /// </summary>
//     /// <typeparam name="TState">The type of the state to use with the middleware.</typeparam>
//     /// <param name="services">The service collection.</param>
//     /// <returns>The service collection for chaining.</returns>
//     public static IServiceCollection AddAsyncEffectRetryMiddleware<TState>(this IServiceCollection services)
//         where TState : class
//     {
//         services.AddSingleton<AsyncEffectRetryMiddleware<TState>>();
//         return services;
//     }
// }
