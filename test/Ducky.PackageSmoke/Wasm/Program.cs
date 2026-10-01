using Ducky;
using Ducky.Blazor;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddSingleton(new PersistenceFailed("smoke", nameof(ActionTypeAttribute), "wasm"));
await builder.Build().RunAsync();
