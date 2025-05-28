using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Demo.BlazorWasm;
using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback;
using Demo.BlazorWasm.Features.Feedback.Effects;
using Demo.BlazorWasm.Features.JsonColoring;
using Demo.BlazorWasm.Features.JsonColoring.Services;
using Ducky.Blazor;
using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.ExceptionHandling;
using Ducky.Middlewares.NoOp;
using Ducky.Middlewares.ReactiveEffect;
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

// Register ducky middlewares
services.AddJsLoggingMiddleware();
services.AddCorrelationIdMiddleware();
services.AddExceptionHandlingMiddleware();
services.AddAsyncEffectMiddleware();
services.AddReactiveEffectMiddleware();

// Register exception handler
services.AddExceptionHandler<NotificationExceptionHandler>();

// Add Ducky with configured middleware pipeline - Option 1: Direct configuration
// TESTING MIDDLEWARES ONE BY ONE
services.AddDuckyWithPipeline(
    configuration,
    (pipeline, serviceProvider) =>
    {
        // Configure the middleware pipeline in the order you want
        pipeline.Use(serviceProvider.GetRequiredService<CorrelationIdMiddleware>());
        // ISSUE: JsLoggingMiddleware causes duplicate key error in Blazor WebAssembly
        // pipeline.Use(serviceProvider.GetRequiredService<JsLoggingMiddleware>());
        pipeline.Use(serviceProvider.GetRequiredService<ExceptionHandlingMiddleware>());
        // Both effect middlewares
        pipeline.Use(serviceProvider.GetRequiredService<AsyncEffectMiddleware>());
        // ISSUE: ReactiveEffectMiddleware causes the app to hang on initialization
        // pipeline.Use(serviceProvider.GetRequiredService<ReactiveEffectMiddleware>());
    });

// Alternative Option 2: Using middleware types
// services.AddDuckyWithMiddleware(
//     typeof(CorrelationIdMiddleware),
//     typeof(JsLoggingMiddleware),
//     typeof(AsyncEffectMiddleware),
//     typeof(ReactiveEffectMiddleware)
// );

// Alternative Option 3: Using fluent builder
// services.AddDucky(builder => builder
//     .UseMiddleware<CorrelationIdMiddleware>()
//     .UseMiddleware<JsLoggingMiddleware>()
//     .UseMiddleware<AsyncEffectMiddleware>()
//     .UseMiddleware<ReactiveEffectMiddleware>()
// );

await builder.Build().RunAsync();
