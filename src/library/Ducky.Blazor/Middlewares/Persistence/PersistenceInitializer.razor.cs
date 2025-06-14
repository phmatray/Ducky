using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// A Blazor component that initializes state persistence functionality.
/// </summary>
public partial class PersistenceInitializer
{
    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // Try to get PersistenceMiddleware from DI container
        PersistenceMiddleware? persistenceMiddleware = ServiceProvider.GetService<PersistenceMiddleware>();

        if (persistenceMiddleware is not null)
        {
            Logger.LogInformation("PersistenceMiddleware resolved successfully. Starting hydration...");

            try
            {
                // Manually trigger hydration after the first render
                // This ensures the JS runtime is ready for LocalStorage access
                await persistenceMiddleware.HydrateAsync().ConfigureAwait(false);

                Logger.LogInformation("Store hydration completed successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to hydrate store from persistence.");
            }
        }
        else
        {
            Logger.LogDebug("PersistenceMiddleware not found in DI container. Persistence hydration skipped.");
        }
    }
}
