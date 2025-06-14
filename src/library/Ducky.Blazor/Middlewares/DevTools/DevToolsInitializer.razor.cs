using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// A Blazor component that initializes Redux DevTools integration.
/// </summary>
public partial class DevToolsInitializer
{
    private ReduxDevToolsModule? _devTools;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // Try to get DevTools from DI container
        // This won't throw if DevTools isn't registered
        _devTools = ServiceProvider.GetService<ReduxDevToolsModule>();

        if (_devTools is not null)
        {
            Logger.LogInformation("ReduxDevToolsModule resolved successfully. Initializing DevTools...");

            // Get store and dispatcher and set them in DevTools
            IStore? store = ServiceProvider.GetService<IStore>();
            IDispatcher? dispatcher = ServiceProvider.GetService<IDispatcher>();

            if (store is not null && dispatcher is not null)
            {
                _devTools.SetStoreAndDispatcher(store, dispatcher);

                // Initialize DevTools after the first render
                // This ensures the JS runtime is ready
                await _devTools.InitAsync().ConfigureAwait(false);

                Logger.LogInformation("DevTools initialized successfully.");
            }
            else
            {
                Logger.LogError(
                    "Could not resolve IStore or IDispatcher from DI container. DevTools will not be available.");
            }
        }
        else
        {
            Logger.LogWarning(
                "ReduxDevToolsModule not found in DI container. DevTools will not be available. "
                + "To enable DevTools, add .AddDevToolsMiddleware() to your store configuration.");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Clean up if needed
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
