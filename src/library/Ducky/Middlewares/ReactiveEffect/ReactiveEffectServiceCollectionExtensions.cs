// using Microsoft.Extensions.DependencyInjection;
//
// namespace Ducky.Middlewares.ReactiveEffect;
//
// /// <summary>
// /// Provides extension methods for registering the ReactiveEffect middleware and related services.
// /// </summary>
// public static class ReactiveEffectServiceCollectionExtensions
// {
//     /// <summary>
//     /// Registers the ReactiveEffect middleware for the given state type.
//     /// </summary>
//     /// <typeparam name="TState">The type of the state to use with the middleware.</typeparam>
//     /// <param name="services">The service collection.</param>
//     /// <returns>The service collection for chaining.</returns>
//     public static IServiceCollection AddReactiveEffectMiddleware<TState>(this IServiceCollection services)
//         where TState : class
//     {
//         services.AddSingleton<ReactiveEffectMiddleware<TState>>();
//         return services;
//     }
// }
