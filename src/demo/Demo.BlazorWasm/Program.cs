using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Demo.BlazorWasm;
using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.JsonColoring;
using Demo.BlazorWasm.Features.JsonColoring.Services;
using Ducky.Blazor;
using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.NoOp;
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
services.AddTransient<IMoviesService, MoviesService>();

// Register ducky middlewares
services.AddNoOpMiddleware();
services.AddJsLoggingMiddleware();
// services.AddAsyncEffectMiddleware();

// Add Ducky
services.AddDucky(
    configuration,
    options =>
    {
        // Compose the middleware pipeline IN THE ORDER YOU WANT:
        options.ConfigurePipeline = pipeline =>
        {
            pipeline.Use<NoOpMiddleware>();
            pipeline.Use<JsLoggingMiddleware>();
            // pipeline.Use<AsyncEffectMiddleware>();
        };
    });

await builder.Build().RunAsync();
