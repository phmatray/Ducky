using System.Text.Json.Nodes;
using Ducky.Blazor;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Samples.Server.Client;

/// <summary>The one Ducky registration both sides start from (SPEC §11.3, Interactive Auto).</summary>
public static class SampleRegistration
{
    /// <summary>The app-unique storage prefix: required by PersistStorage.Server (DUCKY317), shared by both sides.</summary>
    public const string KeyPrefix = "ducky-samples";

    /// <summary>
    /// Adds the store. The server lifetime is Scoped and the browser's Singleton (the default per host). The Server-storage
    /// <see cref="NotesSlice"/> is inert on the WASM side, which registers neither IDistributedCache nor an
    /// AuthenticationStateProvider. DevTools is always on: it stays inert until the extension is present.
    /// </summary>
    public static IServiceCollection AddDuckySample(this IServiceCollection services) =>
        services.AddDucky(ducky => ducky
            .AddDuckyGenerated_Ducky_Samples_Server_Client()
            .UseJson(SampleJson.Default)
            .AddBlazor(options => options.KeyPrefix = KeyPrefix)
            .UseDevTools(options =>
            {
                options.Name = "Ducky.Samples.Server";
                options.ActionSanitizer = static (type, payload) => type == "draft/changed" ? JsonValue.Create("<redacted>") : payload;
            }));
}
