using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Demo.BlazorWasm;
using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback;
using Demo.BlazorWasm.Features.JsonColoring;
using Demo.BlazorWasm.Features.JsonColoring.Services;
using Ducky.Builder;
using Ducky.Blazor.Middlewares.DevTools;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;
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

// Configure DevTools options
services.TryAddSingleton<DevToolsOptions>(
    _ => new DevToolsOptions
    {
        StoreName = "DuckyDemo",
        Enabled = builder.HostEnvironment.IsDevelopment(),
        ExcludedActionTypes = ["Tick"], // Exclude noisy timer ticks
        MaxAge = 100 // Keep more history for demo
    });

// Register DevTools dependencies
services.TryAddSingleton<DevToolsStateManager>();
services.TryAddScoped<DevToolsReducer>();
services.TryAddScoped<ReduxDevToolsModule>(
    serviceProvider =>
    {
        IJSRuntime jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
        IStore store = serviceProvider.GetRequiredService<IStore>();
        IDispatcher dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
        DevToolsStateManager stateManager = serviceProvider.GetRequiredService<DevToolsStateManager>();
        DevToolsOptions options = serviceProvider.GetRequiredService<DevToolsOptions>();
        return new ReduxDevToolsModule(jsRuntime, store, dispatcher, stateManager, options);
    });

// Add Ducky with the new StoreBuilder approach
services.AddDuckyStore(storeBuilder => storeBuilder
    // Add middlewares in order
    .AddCorrelationIdMiddleware()
    .AddExceptionHandlingMiddleware()
    .AddAsyncEffectMiddleware()
    // ISSUE: JsLoggingMiddleware causes duplicate key error in Blazor WebAssembly
    // .AddMiddleware<Ducky.Blazor.Middlewares.JsLogging.JsLoggingMiddleware>()
    // ISSUE: ReactiveEffectMiddleware causes the app to hang on initialization
    // .AddReactiveEffectMiddleware()
    
    // Add Redux DevTools for debugging
    .AddMiddleware<DevToolsMiddleware>(sp =>
    {
        ReduxDevToolsModule devTools = sp.GetRequiredService<ReduxDevToolsModule>();
        IStore store = sp.GetRequiredService<IStore>();
        return new DevToolsMiddleware(devTools, store);
    })
    
    // Add exception handler
    .AddExceptionHandler<NotificationExceptionHandler>()
);

await builder.Build().RunAsync();
