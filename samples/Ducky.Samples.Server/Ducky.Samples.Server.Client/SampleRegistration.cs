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
    /// <see cref="NotesSlice"/> is inert on the WASM side, which registers no IDistributedCache, and no
    /// AuthenticationStateProvider except on the /wasm-user document (see Program.cs): there a client-side provider and a
    /// NameIdentifier browser <paramref name="scope"/> make every user change a scope switch, which resets the notes without
    /// reading them (SPEC §11.6). DevTools is always on: it stays inert until the extension is present, and the draft text
    /// never reaches it, neither as an action payload nor as state.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="scope">The browser <see cref="BlazorOptions.Scope"/>; only the /wasm-user document sets one.</param>
    public static IServiceCollection AddDuckySample(this IServiceCollection services, Func<IServiceProvider, CancellationToken, ValueTask<string?>>? scope = null) =>
        services.AddDucky(ducky => ducky
            .AddDuckyGenerated_Ducky_Samples_Server_Client()
            .UseJson(SampleJson.Default)
            .AddBlazor(options => (options.KeyPrefix, options.Scope) = (KeyPrefix, scope))
            .UseDevTools(options =>
            {
                options.Name = "Ducky.Samples.Server";
                options.ActionSanitizer = static (type, payload) => type == "draft/changed" ? JsonValue.Create("<redacted>") : payload;
                options.StateSanitizer = static (key, state) => key == "draft" ? JsonValue.Create("<redacted>") : state;
            }));
}
