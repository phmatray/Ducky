using DuckShop;
#region program-usings
using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
#endregion
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

#region program-catalogue-api
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICatalogueApi, FakeCatalogueApi>();
#endregion
#region program-search-api
builder.Services.AddSingleton<ISearchApi, FakeSearchApi>();
#endregion

#region program-store
builder.Services.AddDucky(ducky => ducky
    .AddDuckyGenerated_DuckShop() // every slice and effect of this app
    .AddBlazor()                  // Ducky's Blazor services
    .UseJson(ShopJson.Default)    // how state is written to storage
    .PersistBasket());            // the basket survives a reload
#endregion

await builder.Build().RunAsync();
