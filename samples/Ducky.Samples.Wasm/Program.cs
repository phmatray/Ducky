using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Ducky.Samples.Wasm;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddDucky(ducky =>
{
    // Every slice, SliceStore and effect of this assembly, and the [Persist] defaults (GEN-02).
    ducky.AddDuckyGenerated_Ducky_Samples_Wasm().UseJson(SampleJson.Default);

    // DevTools is never enabled by the library itself (ADR-0043): the app opts in, in Development only.
    // A publish runs as Production; wwwroot/index.html switches it to Development for a page loaded with `?devtools`.
    if (builder.HostEnvironment.IsDevelopment())
    {
        ducky.UseDevTools(options =>
        {
            options.Name = "Ducky.Samples.Wasm";
            options.Trace = true;
        });
    }
});

await builder.Build().RunAsync();
