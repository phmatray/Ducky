// using Microsoft.Extensions.DependencyInjection;
//
// namespace Ducky.Blazor.Middlewares.Persistence;
//
// /// <summary>
// /// Provides extension methods for registering the persistence middleware
// /// and related services in the service collection.
// /// </summary>
// public static class PersistenceServiceCollectionExtensions
// {
//     /// <summary>
//     /// Registers the Persistence Middleware, HydrationManager, and a persistence provider for the given state type.
//     /// </summary>
//     /// <typeparam name="TState">The type of the state to persist.</typeparam>
//     /// <typeparam name="TProvider">The type of the persistence provider.</typeparam>
//     /// <param name="services">The service collection.</param>
//     /// <returns>The service collection for chaining.</returns>
//     public static IServiceCollection AddPersistenceMiddleware<TState, TProvider>(this IServiceCollection services)
//         where TState : class
//         where TProvider : class, IPersistenceProvider<TState>
//     {
//         services.AddSingleton<HydrationManager>();
//         services.AddSingleton<IPersistenceProvider<TState>, TProvider>();
//         services.AddSingleton<PersistenceMiddleware<TState>>();
//         return services;
//     }
// }
