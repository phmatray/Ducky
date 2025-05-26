// using Microsoft.Extensions.DependencyInjection;
//
// namespace Ducky.Middlewares.CorrelationId;
//
// /// <summary>
// /// Provides extension methods for registering the CorrelationId middleware and related services.
// /// </summary>
// public static class CorrelationIdServiceCollectionExtensions
// {
//     /// <summary>
//     /// Registers the CorrelationId middleware.
//     /// </summary>
//     /// <param name="services">The service collection.</param>
//     /// <returns>The service collection for chaining.</returns>
//     public static IServiceCollection AddCorrelationIdMiddleware(this IServiceCollection services)
//     {
//         services.AddSingleton<CorrelationIdMiddleware>();
//         return services;
//     }
// }
