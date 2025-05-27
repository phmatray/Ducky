using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Demo.BlazorWasm;
using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback.Effects;
using Demo.BlazorWasm.Features.JsonColoring;
using Demo.BlazorWasm.Features.JsonColoring.Services;
using Ducky.Blazor;
using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.NoOp;
using Ducky.Middlewares.ReactiveEffect;
using Ducky.Middlewares.AsyncEffectRetry;
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
services.AddCorrelationIdMiddleware();
services.AddAsyncEffectMiddleware();
services.AddAsyncEffectRetryMiddleware();
services.AddReactiveEffectMiddleware();

// Register async effects (for retry demonstration)
services.AddAsyncEffect<RetryableMoviesEffect>();

// Register reactive effects
services.AddReactiveEffect<AllActionsEffect>();
services.AddReactiveEffect<LoadMoviesSuccessEffect>();
services.AddReactiveEffect<LoadMoviesFailureEffect>();
services.AddReactiveEffect<OpenAboutDialogEffect>();
services.AddReactiveEffect<TimerTickEffect>();
services.AddReactiveEffect<DebouncedSearchEffect>();

// Add Ducky
services.AddDucky(
    configuration,
    options =>
    {
        // The middleware services are not yet available here, so we'll let Ducky handle the pipeline configuration
        // Middlewares will be registered but not used in the pipeline for now
    });

await builder.Build().RunAsync();
