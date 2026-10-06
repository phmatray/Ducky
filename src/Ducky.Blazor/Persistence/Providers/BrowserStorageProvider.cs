using Microsoft.Extensions.Logging;

namespace Ducky.Blazor;

// Local and Session storage through ducky.js (SPEC §11.5): every value crosses as a string (INV-23).
internal sealed class BrowserStorageProvider(JsBridge bridge, BlazorOptions options, ILogger logger)
{
    // What ducky.js's storageGet answers above InlinePayloadBytes (§11.9): not an envelope, never a stored value.
    internal const string TooLarge = "\u0000ducky:too-large";

    private readonly SafeLogger _logger = new(logger);

    // The value, or null when nothing is stored. An interrupted call (a disconnect, an interop timeout or the token) throws:
    // a read that never happened is not "not found", so it fails the attempt rather than restore nothing (§11.5 step 7).
    // ponytail: a value above InlinePayloadBytes reads as not found (Debug 2040) until the stream path (§11.5, D11) lands.
    public async ValueTask<string?> GetAsync(PersistStorage storage, string key, CancellationToken cancellationToken)
    {
        var area = storage == PersistStorage.Local ? "local" : "session";
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        var (delivered, value) = await bridge.TryInvokeAsync<string>("storageGet", cancellationToken, area, key, options.InlinePayloadBytes).ConfigureAwait(false);
        if (!delivered)
        {
            throw new InvalidOperationException($"Reading '{key}' from {area} storage was interrupted (a disconnect or an interop timeout).");
        }

        if (value == TooLarge)
        {
            Log.TooLargeReadSkipped(_logger, key, options.InlinePayloadBytes);
            return null;
        }

        return value;
    }
}
