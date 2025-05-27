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

// Add Ducky with configured middleware pipeline - Option 1: Direct configuration
services.AddDuckyWithPipeline(
    configuration,
    (pipeline, serviceProvider) =>
    {
        // Configure the middleware pipeline in the order you want
        pipeline.Use(serviceProvider.GetRequiredService<NoOpMiddleware>());
        pipeline.Use(serviceProvider.GetRequiredService<CorrelationIdMiddleware>());
        pipeline.Use(serviceProvider.GetRequiredService<JsLoggingMiddleware>());
        pipeline.Use(serviceProvider.GetRequiredService<AsyncEffectMiddleware>());
        pipeline.Use(serviceProvider.GetRequiredService<AsyncEffectRetryMiddleware>());
        pipeline.Use(serviceProvider.GetRequiredService<ReactiveEffectMiddleware>());
    });

// Alternative Option 2: Using middleware types
// services.AddDuckyWithMiddleware(
//     typeof(NoOpMiddleware),
//     typeof(CorrelationIdMiddleware),
//     typeof(JsLoggingMiddleware),
//     typeof(AsyncEffectMiddleware),
//     typeof(AsyncEffectRetryMiddleware),
//     typeof(ReactiveEffectMiddleware)
// );

// Alternative Option 3: Using fluent builder
// services.AddDucky(builder => builder
//     .UseMiddleware<NoOpMiddleware>()
//     .UseMiddleware<CorrelationIdMiddleware>()
//     .UseMiddleware<JsLoggingMiddleware>()
//     .UseMiddleware<AsyncEffectMiddleware>()
//     .UseMiddleware<AsyncEffectRetryMiddleware>()
//     .UseMiddleware<ReactiveEffectMiddleware>()
// );

await builder.Build().RunAsync();
