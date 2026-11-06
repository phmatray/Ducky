using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Ducky.E2E.Persistence;

// What the persistence, prerender and circuit specs share against samples/Ducky.Samples.Server (SPEC §17.6).
internal static partial class ServerPages
{
    public const string Sample = "Ducky.Samples.Server";

    // A cold host on a 2-vCPU runner: circuit start, store init and hydration before a page turns interactive.
    public const float InteractiveTimeoutMs = 30_000;

    // localStorage key of a slice: {KeyPrefix}:{key}, no scope segment (SampleRegistration).
    public const string LargeKey = "ducky-samples:large";

    public static Task<PublishedHost> StartAsync() => PublishedHost.StartSampleAsync(Sample, "/health");

    public static Task<IResponse?> GotoAsync(this IPage page, PublishedHost host, string path) =>
        page.GotoAsync(new Uri(host.BaseUrl, path).ToString());

    public static Task<string> LoadsAsync(PublishedHost host) => GetAsync(host, "/api/products/loads");

    public static async Task<string> GetAsync(PublishedHost host, string path)
    {
        using var http = new HttpClient { BaseAddress = host.BaseUrl };
        return await http.GetStringAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
    }

    // blazor.web.js resolves an unassigned Interactive Auto component again on every refresh until a renderer is attached,
    // and picks WebAssembly once the runtime has loaded: a first visit runs on a circuit only if the circuit connects
    // before the background download ends. A cold host on a loaded 2-vCPU runner can lose that race to a loopback
    // download, and a circuit resumed after the download re-resolves its Auto components to WebAssembly. Holding the
    // runtime's .wasm downloads until the returned release runs makes the circuit the only renderer an Auto page can get,
    // as on a real first visit, whose download is slower than the circuit's start.
    public static async Task<Action> HoldWebAssemblyAsync(IPage page)
    {
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync("**/_framework/*.wasm", async route =>
        {
            await released.Task;
            await route.ContinueAsync();
        });
        return () => released.TrySetResult();
    }

    public static ConcurrentQueue<string> CollectErrors(IPage page)
    {
        var errors = new ConcurrentQueue<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Enqueue(message.Text);
            }
        };
        page.PageError += (_, error) => errors.Enqueue(error);
        return errors;
    }

    // Leaving an Interactive Auto page while its background WebAssembly download runs aborts the download, which Firefox
    // and WebKit each log as errors of their own wording. blazor.web.js records the runtime's resource hash in localStorage
    // once the runtime has loaded: waiting for it before leaving leaves nothing to abort, so no error needs dropping and
    // every error logged in a test stays to be asserted. A held download (HoldWebAssemblyAsync) must be released first.
    public static Task WaitForWebAssemblyAsync(IPage page) =>
        page.WaitForFunctionAsync("() => Object.keys(localStorage).some(k => k.startsWith('blazor-resource-hash:'))",
            null, new() { PollingInterval = 50, Timeout = InteractiveTimeoutMs });

    // The ducky:seed envelope in a prerendered page's WebAssembly component state, which is not data-protected (unlike the
    // Server copy): base64 of the state dictionary, whose entry is PersistAsJson<byte[]> of the UTF-8 envelope (SPEC §11.4).
    // Null when the page persisted no seed.
    public static JsonElement? Seed(string html)
    {
        var state = WasmState().Match(html);
        if (!state.Success)
        {
            return null;
        }
        using var dictionary = JsonDocument.Parse(Convert.FromBase64String(state.Groups[1].Value));
        if (!dictionary.RootElement.TryGetProperty("ducky:seed", out var entry))
        {
            return null;
        }
        var bytes = JsonSerializer.Deserialize<byte[]>(Encoding.UTF8.GetString(Convert.FromBase64String(entry.GetString()!)))!;
        using var envelope = JsonDocument.Parse(bytes);
        return envelope.RootElement.Clone();
    }

    public static bool Carries(this JsonElement? seed, string key) =>
        seed is { } envelope && envelope.GetProperty("slices").TryGetProperty(key, out _);

    [GeneratedRegex("<!--Blazor-WebAssembly-Component-State:([A-Za-z0-9+/=]+)-->")]
    private static partial Regex WasmState();
}
