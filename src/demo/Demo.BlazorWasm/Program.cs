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

// Register all application slice reducers
services.TryAddScoped<ISlice<CounterState>, CounterReducers>();
services.TryAddScoped<ISlice<GoalState>, GoalsReducers>();
services.TryAddScoped<ISlice<LayoutState>, LayoutReducers>();
services.TryAddScoped<ISlice<MessageState>, MessageReducers>();
services.TryAddScoped<ISlice<MoviesState>, MoviesReducers>();
services.TryAddScoped<ISlice<NotificationsState>, NotificationsReducers>();
services.TryAddScoped<ISlice<ProductState>, ProductsReducers>();
services.TryAddScoped<ISlice<TimerState>, TimerReducers>();
services.TryAddScoped<ISlice<TodoState>, TodoReducers>();

// Add Ducky with the new StoreBuilder approach
services.AddDuckyStore(storeBuilder => storeBuilder
    // Add middlewares in the correct order
    .AddCorrelationIdMiddleware()
    .AddExceptionHandlingMiddleware()
    .AddAsyncEffectMiddleware()
    .AddReactiveEffectMiddleware() // Fixed: now uses lazy initialization

    // Add Redux DevTools for debugging (with simplified API)
    .AddDevToolsMiddleware(options =>
    {
        options.StoreName = "DuckyDemo";
        options.Enabled = builder.HostEnvironment.IsDevelopment();
        options.ExcludedActionTypes = ["Tick"]; // Exclude noisy timer ticks
        options.MaxAge = 100; // Keep more history for demo
    })

    // Register async effects
    .AddEffect<ResetCounterAfter3Sec>()
    
    // Register reactive effects (now fixed with lazy initialization)
    .AddReactiveEffect<StartTimerEffect>()

    // Add exception handler
    .AddExceptionHandler<NotificationExceptionHandler>()
);

await builder.Build().RunAsync();
