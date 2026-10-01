using Microsoft.Extensions.DependencyInjection;

namespace Ducky;

/// <summary>Registers Ducky in an <see cref="IServiceCollection"/>.</summary>
public static class DuckyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the store. Call it once per <see cref="IServiceCollection"/>: a second call throws a
    /// <see cref="DuckyConfigurationException"/> (DUCKY300). Every other rule is checked together at the first store
    /// resolution, which throws one <see cref="DuckyConfigurationException"/> listing every problem.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adds slices and sets options.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="DuckyConfigurationException">AddDucky was already called on <paramref name="services"/> (DUCKY300).</exception>
    public static IServiceCollection AddDucky(this IServiceCollection services, Action<DuckyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ThrowIfRegistered(services);
        var builder = new DuckyBuilder(services);
        configure(builder);

        // Checked again: an AddDucky nested in configure registered first, so this call is the second one.
        ThrowIfRegistered(services);

        // Frozen after configure returns, so an IsBrowser or Lifetime set late still decides the lifetime (§5.1).
        var config = builder.Freeze();
        services.AddSingleton(_ => new DuckyConfig.ValidationState()); // a factory: one per container, never shared
        services.Add(ServiceDescriptor.Describe(typeof(DuckyStore), config.Build, config.Lifetime));
        return services;
    }

    private static void ThrowIfRegistered(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(DuckyStore)))
        {
            throw new DuckyConfigurationException([DuckyErrors.AddDuckyTwice()]);
        }
    }
}
