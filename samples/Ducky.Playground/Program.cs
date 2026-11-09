using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Ducky.Playground;
using Ducky.Playground.Inspector;
using Ducky.Playground.Shop.Account;
using Ducky.Playground.Shop.Api;
using Ducky.Playground.Shop.Cart;
using Ducky.Playground.Shop.Orders;
using Ducky.Playground.Xray;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ShopApi>();
builder.Services.AddScoped<SourceFiles>();

// The inspector: the tape, and the listener on the store's drop counters (the store records metrics through the
// container's IMeterFactory, so AddMetrics turns them on).
builder.Services.AddMetrics();
builder.Services.AddSingleton<Tape>();
builder.Services.AddSingleton<DropMeter>();

builder.Services.AddDucky(ducky =>
{
    // Every slice, SliceStore, effect and reactive effect of this assembly (GEN-02), serialized through one
    // source-generated JSON context: persistence, DevTools and the inspector all read it (no reflection, trim-safe).
    ducky.AddDuckyGenerated_Ducky_Playground()
        .UseJson(PlaygroundJson.Default)
        .AddBlazor(options => options.KeyPrefix = CartPersistence.KeyPrefix)
        .PersistCart()
        .Persist<OrdersSlice>(options => options.SyncAcrossTabs = true)
        .Persist<ProfileSlice>()
        .Persist<XrayStore>()
        // Middleware is never generated: registered in order, the audit first so it sees every action.
        .Use<AuditMiddleware>()
        .Use<QuantityLimitMiddleware>();

    // DevTools is never enabled by the library (ADR-0043), nor by default here: wwwroot/index.html starts the app in
    // Development only for a page opened with ?devtools.
    if (builder.HostEnvironment.IsDevelopment())
    {
        ducky.UseDevTools(options => options.Name = "Ducky Playground");
    }
});

var host = builder.Build();
// Listening before the store exists, so its counters are heard from the first action.
_ = host.Services.GetRequiredService<DropMeter>();
await host.RunAsync();
