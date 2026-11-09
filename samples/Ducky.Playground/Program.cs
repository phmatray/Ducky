// The whole store is configured here: the generated registrations, one JSON context, the
// persisted slices, and the middleware, in the order they run.
using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Ducky.Playground;
using Ducky.Playground.Inspector;
using Ducky.Playground.Shop.Account;
using Ducky.Playground.Shop.Api;
using Ducky.Playground.Shop.Basket;
using Ducky.Playground.Shop.Orders;
using Ducky.Playground.Xray;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = baseAddress });
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ShopApi>();
builder.Services.AddScoped<SourceFiles>();

// The inspector: its tape, and the listener on the store's drop counters. The store
// records metrics through the container's IMeterFactory, so AddMetrics turns them on.
builder.Services.AddMetrics();
builder.Services.AddSingleton<Tape>();
builder.Services.AddSingleton<DropMeter>();

builder.Services.AddDucky(ducky =>
{
    // Every slice, SliceStore, effect and reactive effect of this assembly is
    // registered by the generator. One source-generated JSON context serves
    // persistence, DevTools and the inspector: no reflection, so it stays trim-safe.
    ducky.AddDuckyGenerated_Ducky_Playground()
        .UseJson(PlaygroundJson.Default)
        .AddBlazor(options => options.KeyPrefix = BasketPersistence.KeyPrefix)
        .PersistBasket()
        .Persist<OrdersSlice>(options => options.SyncAcrossTabs = true)
        .Persist<ProfileSlice>()
        .Persist<XrayStore>()
        // Middleware is never generated. It runs in registration order, so the audit
        // comes first and sees every action.
        .Use<AuditMiddleware>()
        .Use<QuantityLimitMiddleware>();

    // The library never enables DevTools (ADR-0043), nor does the shop by default:
    // wwwroot/index.html starts in Development only for a page opened with ?devtools.
    if (builder.HostEnvironment.IsDevelopment())
    {
        ducky.UseDevTools(options => options.Name = "Ducky Playground");
    }
});

var host = builder.Build();

// Resolved before the store exists, so the listener hears its counters from the start.
_ = host.Services.GetRequiredService<DropMeter>();
await host.RunAsync();
