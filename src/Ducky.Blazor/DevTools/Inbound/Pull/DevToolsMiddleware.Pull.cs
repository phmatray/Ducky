using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor;

// SPEC §11.8 inbound, large messages (INV-23, D11): a message above InlinePayloadBytes stays in ducky.js under (id, seq)
// and is pulled once through devtoolsTakeMessage by stream. The reference is disposed on every path; one NUL byte (already
// taken or pruned), a message taken while inert (never read, so ducky.js drops it), a message above MaxPayloadBytes
// (Warning 2023, decided from Length before opening, spike S-5), and a pull that was rejected or interrupted all read as
// no message, ignored with the Debug log of any other (2065).
internal sealed partial class DevToolsMiddleware
{
    private async Task<string?> PullAsync(long seq)
    {
        string? json = null;
        try
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            var (delivered, pulled) = await _bridge.TryInvokeAsync<IJSStreamReference>("devtoolsTakeMessage", CancellationToken.None, _persistence.Id, seq).ConfigureAwait(false);
            if (delivered)
            {
                // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                json = await ReadAsync(pulled!, seq).ConfigureAwait(false); // a delivered pull: Blazor never hands back a null reference
            }
        }
        catch (JSException)
        {
            // The runtime rejected the pull (as S-5 measured for an export's return): no message. An interrupted one comes
            // back undelivered; anything else faults the invocation, which ducky.js counts as a failed delivery (§11.7).
        }

        return json;
    }

    private async Task<string?> ReadAsync(IJSStreamReference reference, long seq)
    {
        try
        {
            if (reference.Length <= 1 || Volatile.Read(ref _connected) == 0)
            {
                return null;
            }

            // maxAllowedSize below is only a backstop (S-5).
            if (reference.Length > _blazor.MaxPayloadBytes)
            {
                MessageTooLarge(_log, seq, reference.Length, _blazor.MaxPayloadBytes);
                return null;
            }

            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            using var reader = new StreamReader(await reference.OpenReadStreamAsync(_blazor.MaxPayloadBytes).ConfigureAwait(false));
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        finally
        {
            // The await using of INV-23; a dropped circuit never makes the disposal throw (§11.9).
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            await DisposeQuietlyAsync(reference).ConfigureAwait(false);
        }
    }

    private async ValueTask DisposeQuietlyAsync(IJSStreamReference reference)
    {
        try
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            await reference.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or TaskCanceledException)
        {
            Log.InteropInterrupted(_log, exception, "dispose");
        }
    }

    // 2023, as for a stored value above MaxPayloadBytes (§11.5); declared here because one class can't hold two methods
    // with the same EventId (SYSLIB1006).
    [LoggerMessage(EventId = 2023, Level = LogLevel.Warning, Message = "Redux DevTools message {Seq} is {Length} bytes, above MaxPayloadBytes ({MaxPayloadBytes}); ignored")]
    private static partial void MessageTooLarge(SafeLogger logger, long seq, long length, int maxPayloadBytes);
}
