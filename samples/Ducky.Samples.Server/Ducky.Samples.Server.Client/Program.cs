using Ducky.Samples.Server.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);
builder.Services.AddDuckySample();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = baseAddress });
builder.Services.AddScoped<IProductCatalog, HttpProductCatalog>();
builder.Services.AddTransient<DispatchingHandler>();
builder.Services.AddHttpClient("api", client => client.BaseAddress = baseAddress).AddHttpMessageHandler<DispatchingHandler>();
await builder.Build().RunAsync();
