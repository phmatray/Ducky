using Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

// The WASM host as the Blazor Web App template builds it (auth-state deserialization), with the store a singleton
// (SPEC §6.10, browser lifetime).
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
builder.Services.AddSingleton<SpikeStore>();
SpikeInfo.Descriptors = [.. builder.Services];
var host = builder.Build();
// Not the root: SpikeStore records the root it is constructed from (S-7 answer 1 compares the two).
SpikeInfo.HostServices = host.Services;
await host.RunAsync();
