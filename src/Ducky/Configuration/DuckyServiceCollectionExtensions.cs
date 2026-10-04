using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ducky;

/// <summary>Registers Ducky in an <see cref="IServiceCollection"/>.</summary>
public static class DuckyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the store as <see cref="IStore"/> and <see cref="IDispatcher"/>, and each slice type as its store-owned
    /// instance, all with <see cref="DuckyBuilder.Lifetime"/>; adds <see cref="TimeProvider.System"/> when no
    /// <see cref="TimeProvider"/> is registered. Call it once per <see cref="IServiceCollection"/>: a second call throws a
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
        var lifetime = config.Lifetime;
        services.AddSingleton(_ => new DuckyConfig.ValidationState()); // a factory: one per container, never shared
        services.Add(ServiceDescriptor.Describe(typeof(IStore), sp => DuckyStore.Create(sp, config), lifetime));
        services.Add(ServiceDescriptor.Describe(typeof(IDispatcher), sp => sp.GetRequiredService<IStore>(), lifetime));
        foreach (var type in builder.SliceTypes)
        {
            services.Add(ServiceDescriptor.Describe(type, sp => sp.GetRequiredService<IStore>().Slices.First(slice => slice.GetType() == type), lifetime));
        }

        // Resolved last at a Scoped store's materialization, so the scope disposes it first (§6.11 server disposal order).
        services.AddScoped(sp => new StoreDisposeHook(sp.GetRequiredService<IStore>()));
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    // Keyed on an internal type no app can register, so an app's own IStore (decorator, keyed, double) is not mistaken
    // for a second AddDucky.
    private static void ThrowIfRegistered(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(DuckyConfig.ValidationState)))
        {
            throw new DuckyConfigurationException([DuckyErrors.AddDuckyTwice()]);
        }
    }
}
