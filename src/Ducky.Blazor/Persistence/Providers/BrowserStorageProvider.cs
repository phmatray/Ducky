using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor;

// Local and Session storage through ducky.js (SPEC §11.5): every argument crosses as a string or an int (INV-23); a value
// above InlinePayloadBytes comes back through an IJSStreamReference.
internal sealed class BrowserStorageProvider(JsBridge bridge, BlazorOptions options, ILogger logger)
{
    // What ducky.js's storageGet answers above InlinePayloadBytes (§11.9): not an envelope, never a stored value.
    internal const string TooLarge = "\u0000ducky:too-large";

    private readonly SafeLogger _logger = new(logger);

    // The value, or null when nothing is stored. An interrupted call (a disconnect, an interop timeout or the token) throws:
    // a read that never happened is not "not found", so it fails the attempt rather than restore nothing (§11.5 step 7).
    public async ValueTask<string?> GetAsync(PersistStorage storage, string key, CancellationToken cancellationToken)
    {
        var area = storage == PersistStorage.Local ? "local" : "session";
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        var (delivered, value) = await bridge.TryInvokeAsync<string>("storageGet", cancellationToken, area, key, options.InlinePayloadBytes).ConfigureAwait(false);
        ThrowIfInterrupted(delivered, area, key);
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        return value == TooLarge ? await PullAsync(area, key, cancellationToken).ConfigureAwait(false) : value;
    }

    // A value above InlinePayloadBytes crosses by stream (D11, spike S-5). The reference is disposed after it is read, on
    // every path, so its Uint8Array is never left pinned in the circuit's JS object table.
    private async ValueTask<string?> PullAsync(string area, string key, CancellationToken cancellationToken)
    {
        bool delivered;
        IJSStreamReference? pulled;
        try
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            (delivered, pulled) = await bridge.TryInvokeAsync<IJSStreamReference>("storageGetStream", cancellationToken, area, key).ConfigureAwait(false);
        }
        catch (JSException exception)
        {
            // "A failed pull is caught per key and never fails the attempt" (§11.5 step 4): the runtime rejected the pull
            // (S-5 measured it rejecting an export's return), so this key reads as not found and the others still hydrate.
            Log.PullFailed(_logger, exception, key);
            return null;
        }

        // An interrupted pull (a disconnect, an interop timeout or the token) is not a failed one: as for storageGet, a read
        // that never happened fails the attempt (step 7), or the slice's load would run and replace the stored value.
        ThrowIfInterrupted(delivered, area, key);
        var reference = pulled!; // a delivered pull: Blazor never hands back a null reference
        try
        {
            // One NUL byte: the key was removed between the two calls. An envelope is never shorter than 2 bytes.
            if (reference.Length <= 1)
            {
                return null;
            }

            // Decided from Length, before opening: maxAllowedSize below is only a backstop (S-5), left uncaught (§11.5).
            if (reference.Length > options.MaxPayloadBytes)
            {
                Log.PayloadTooLarge(_logger, key, reference.Length, options.MaxPayloadBytes);
                return null;
            }

            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            using var reader = new StreamReader(await reference.OpenReadStreamAsync(options.MaxPayloadBytes, cancellationToken).ConfigureAwait(false));
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // The await using of §11.5 and INV-23, through DisposeAsync below so a dropped circuit can't make it throw.
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            await DisposeAsync(reference).ConfigureAwait(false);
        }
    }

    private static void ThrowIfInterrupted(bool delivered, string area, string key)
    {
        if (!delivered)
        {
            throw new InvalidOperationException($"Reading '{key}' from {area} storage was interrupted (a disconnect or an interop timeout).");
        }
    }

    // §11.9: every .NET-side disposal of an IJSStreamReference catches JSDisconnectedException, as JsBridge's module disposal
    // does: a circuit dropped after the pull delivered never turns a read that settled into a failed attempt.
    private async ValueTask DisposeAsync(IJSStreamReference reference)
    {
        try
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            await reference.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or TaskCanceledException)
        {
            Log.InteropInterrupted(_logger, exception, "dispose");
        }
    }
}
