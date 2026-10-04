using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor;

/// <summary>Registers Ducky.Blazor on a <see cref="DuckyBuilder"/>.</summary>
public static class DuckyBlazorBuilderExtensions
{
    /// <summary>
    /// Adds Ducky.Blazor to the store. Registration is idempotent: the first call registers the prerender handoff and then
    /// the persistence middleware, at fixed positions. Configuration composes: every <paramref name="configure"/> of every
    /// call is applied, in call order, to one <see cref="BlazorOptions"/>, so the last value written to a property wins.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Sets options; <see langword="null"/> only registers.</param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static DuckyBuilder AddBlazor(this DuckyBuilder builder, Action<BlazorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // The first call is told by a marker only AddBlazor registers: a BlazorOptions the app registered does not count.
        if (builder.Services.FirstOrDefault(static d => d.ServiceType == typeof(BlazorRegistration))?.ImplementationInstance is not BlazorRegistration(var options))
        {
            options = new BlazorOptions();
            builder.Services.AddSingleton(new BlazorRegistration(options)).AddSingleton(options);
            // Stryker disable once Statement : the placeholders are inert until M6-04 and M6-07 fill them; their positions are asserted there
            builder.Use<PrerenderHandoff>().Use<PersistenceMiddleware>();
        }

        configure?.Invoke(options);
        return builder;
    }
}

internal sealed record BlazorRegistration(BlazorOptions Options);
