using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Demo.BlazorWasm;
using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback;
using Demo.BlazorWasm.Features.JsonColoring;
using Demo.BlazorWasm.Features.JsonColoring.Services;
using Ducky.Blazor.Builder;
using Ducky.Builder;
using MudBlazor.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
WebAssemblyHostConfiguration configuration = builder.Configuration;
IServiceCollection services = builder.Services;

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add front services
services.AddMudServices(
    config =>
    {
        config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
        config.SnackbarConfiguration.ShowCloseIcon = true;
    });

// Add business services
services.AddScoped<IJsonColorizer, JsonColorizer>();
services.AddScoped<IMoviesService, MoviesService>();

// Add Ducky with the new StoreBuilder approach
services.AddDuckyStore(storeBuilder => storeBuilder
    // Add middlewares in the correct order
    .AddCorrelationIdMiddleware()
    .AddAsyncEffectMiddleware()

    // Add Redux DevTools for debugging (with simplified API)
    // .AddDevToolsMiddleware(options =>
    // {
    //     options.StoreName = "DuckyDemo";
    //     options.Enabled = builder.HostEnvironment.IsDevelopment();
    //     options.ExcludedActionTypes = ["Tick"]; // Exclude noisy timer ticks
    //     options.MaxAge = 100; // Keep more history for demo
    // })

    // Add JS Logging middleware
    .AddJsLoggingMiddleware()

    // Add exception handler
    .AddExceptionHandler<NotificationExceptionHandler>()
);

await builder.Build().RunAsync();
