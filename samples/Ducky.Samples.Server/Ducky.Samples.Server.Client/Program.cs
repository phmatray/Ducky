using System.Runtime.InteropServices.JavaScript;
using Ducky.Blazor;
using Ducky.Samples.Server.Client;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);
// The /wasm-user document signs in client-side and scopes its browser keys by that user (SPEC §11.6, spike S-7); every
// other document keeps the Interactive Auto WASM side without an AuthenticationStateProvider (§11.6, DUCKY315 skipped).
// ponytail: chosen per document at startup, so /wasm-user needs a full page load; a WASM app with real sign-in has one setup.
if (OperatingSystem.IsBrowser()
    && JSHost.GlobalThis.GetPropertyAsJSObject("location")?.GetPropertyAsString("pathname") == "/wasm-user")
{
    builder.Services.AddDuckySample(DuckyScopes.NameIdentifier);
    builder.Services.AddScoped<ClientSignIn>();
    builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ClientSignIn>());
}
else
{
    builder.Services.AddDuckySample();
}
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = baseAddress });
builder.Services.AddScoped<IProductCatalog, HttpProductCatalog>();
builder.Services.AddTransient<DispatchingHandler>();
builder.Services.AddHttpClient("api", client => client.BaseAddress = baseAddress).AddHttpMessageHandler<DispatchingHandler>();
await builder.Build().RunAsync();
