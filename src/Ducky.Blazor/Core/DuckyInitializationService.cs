using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ducky.Blazor;

// WebAssembly (.NET 11) runs IHostedServices in RunAsync after it restored PersistentComponentState and before it renders
// the root components (measured on Microsoft.AspNetCore.Components.WebAssembly 11.0.0, WebAssemblyHost.RunAsyncCore), so
// init started here finds the prerender seed that a Program.cs preload may miss (§11.4). Browser only: a server store is
// scoped, and its hosted services are app-level. InitializeAsync is idempotent and never faults (ADR-0024), so it is
// fired, never awaited: host start must not wait for hydration, and <DuckyInitializer> may start it again.
internal sealed class DuckyInitializationService(IServiceProvider services, BlazorOptions options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.IsBrowser && options.InitializeOnStart)
        {
            _ = services.GetRequiredService<IStore>().InitializeAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
