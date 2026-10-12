using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using PackageSmoke;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddSingleton(new PersistenceFailed("smoke", nameof(ActionTypeAttribute), "wasm"));
builder.Services.AddDucky(d => d.AddDuckyGenerated_PackageSmoke());
var host = builder.Build();
using (var scope = host.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<IDispatcher>().Increment();
}

await host.RunAsync();
