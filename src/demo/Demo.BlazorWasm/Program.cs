using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Demo.BlazorWasm;
using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback;
using Demo.BlazorWasm.Features.JsonColoring;
using Demo.BlazorWasm.Features.JsonColoring.Services;
using Ducky.Blazor;
using MudBlazor.Services;
using System.Reflection;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
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

// Add Ducky with simplified Blazor API
services.AddDuckyBlazor(ducky => ducky
    // Scan the demo assembly for slices and effects
    .ScanAssemblies(typeof(Program).Assembly)
    
    // Enable JS console logging
    .EnableJsLogging()

    // Add exception handler
    .AddExceptionHandler<NotificationExceptionHandler>()

    // Enable DevTools with configuration
    .EnableDevTools(options =>
    {
        options.StoreName = "DuckyDemo";
        options.ExcludedActionTypes = ["Tick"]; // Exclude noisy timer ticks
        options.MaxAge = 100; // Keep more history for demo
    })

    // Enable persistence to local storage
    .EnablePersistence(options =>
    {
        options.AutoHydrate = true; // Enable auto-hydration
        options.StorageKey = "ducky-demo-state"; // Custom storage key
        options.ThrottleDelayMs = 1000; // Save at most once per second
        options.DebounceDelayMs = 0; // Disable debouncing
        options.EnableLogging = false; // Disable debug logging to prevent console spam
    })
);

await builder.Build().RunAsync();
