using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Demo.BlazorWasm;
using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback;
using Demo.BlazorWasm.Features.JsonColoring;
using Demo.BlazorWasm.Features.JsonColoring.Services;
using Ducky;
using Ducky.Builder;
using Ducky.Blazor.Builder;
using Ducky.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
WebAssemblyHostConfiguration configuration = builder.Configuration;
IServiceCollection services = builder.Services;

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

services.TryAddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add front services
services.AddMudServices(
    config =>
    {
        config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
        config.SnackbarConfiguration.ShowCloseIcon = true;
    });

// Add business services
services.TryAddScoped<IJsonColorizer, JsonColorizer>();
services.TryAddScoped<IMoviesService, MoviesService>();

// Add Ducky with the new StoreBuilder approach
services.AddDuckyStore(storeBuilder => storeBuilder
    // Use production preset which includes standard middlewares
    .UseProductionPreset()
    
    // Add Redux DevTools for debugging (with simplified API)
    .AddDevToolsMiddleware(options =>
    {
        options.StoreName = "DuckyDemo";
        options.Enabled = builder.HostEnvironment.IsDevelopment();
        options.ExcludedActionTypes = ["Tick"]; // Exclude noisy timer ticks
        options.MaxAge = 100; // Keep more history for demo
    })
    
    // Add exception handler
    .AddExceptionHandler<NotificationExceptionHandler>()
);

await builder.Build().RunAsync();
