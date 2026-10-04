using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor;

/// <summary>
/// The only type that calls into JS (SPEC §10, INV-23): every argument is a <see cref="JsArg"/>. It imports ducky.js
/// lazily, once, and keeps the import only while it has not failed (§11.5), so a disconnect during the import never
/// poisons the calls that follow it. A call or a disposal interrupted by a disconnected circuit or an interop timeout
/// reports "not delivered" with a Debug log (EventId 2000).
/// </summary>
internal sealed class JsBridge(IJSRuntime runtime, ILogger logger) : IAsyncDisposable
{
    internal const string ModulePath = "./_content/Ducky.Blazor/ducky.js";

    // JSInterop deserializes each result with System.Text.Json: what its result type must keep under trimming.
    private const DynamicallyAccessedMemberTypes JsonSerialized =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties;

    private readonly SafeLogger _logger = new(logger);
    private readonly Lock _gate = new();
    private Task<IJSObjectReference>? _module;
    private bool _disposed;

    /// <summary>
    /// The pending or successful import, started on first use and again after a faulted or cancelled one. A synchronous
    /// throw (no JS runtime, prerendering) reaches the caller and caches nothing: the interactivity probe reads it (§11.4).
    /// Once the bridge is disposed it throws <see cref="ObjectDisposedException"/> and imports nothing.
    /// </summary>
    internal Task<IJSObjectReference> ImportAsync()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_module is not { IsFaulted: false, IsCanceled: false })
            {
#pragma warning disable RS0030 // justification: JsBridge is the single interop wrapper (§10, INV-23)
                _module = runtime.InvokeAsync<IJSObjectReference>("import", [ModulePath]).AsTask();
#pragma warning restore RS0030
            }

            return _module;
        }
    }

    /// <summary>
    /// Calls a ducky.js export; <c>Delivered</c> is false when a disconnect, an interop timeout or the caller's token
    /// interrupted it (the import included), or, silently, once the bridge is disposed (a late flush, §11.5).
    /// </summary>
    internal async ValueTask<(bool Delivered, T? Value)> TryInvokeAsync<[DynamicallyAccessedMembers(JsonSerialized)] T>(
        string identifier, CancellationToken cancellationToken, params JsArg[] args)
    {
        try
        {
            // The token bounds this caller's wait only: the shared import keeps running for the others.
            var module = await ImportAsync().WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning disable RS0030 // justification: JsBridge is the single interop wrapper (§10, INV-23)
            // The token-free overload keeps JSRuntime.DefaultAsyncTimeout (one given a token, even None, switches it off),
            // so a call pending on a dropped circuit still times out (§8.1); the token bounds only this caller's wait.
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            var value = await module.InvokeAsync<T>(identifier, Array.ConvertAll(args, static arg => arg.Value))
                .AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore RS0030
            return (true, value);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or TaskCanceledException)
        {
            Log.InteropInterrupted(_logger, exception, identifier);
            return (false, default);
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed))
        {
            return (false, default);
        }
    }

    /// <summary>Disposes the imported module, if any, once; a disconnected circuit never makes it throw.</summary>
    public ValueTask DisposeAsync()
    {
        Task<IJSObjectReference>? module;
        lock (_gate)
        {
            _disposed = true;
            module = _module;
            _module = null;
        }

        // ponytail: an import still pending at dispose is not awaited (it could hang on a dead circuit); the circuit's
        // teardown releases its handle.
#pragma warning disable VSTHRD103 // justification: Result of a task that completed successfully never blocks
        return module is { IsCompletedSuccessfully: true } ? DisposeModuleAsync(module.Result) : default;
#pragma warning restore VSTHRD103
    }

    private async ValueTask DisposeModuleAsync(IJSObjectReference module)
    {
        try
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or TaskCanceledException)
        {
            Log.InteropInterrupted(_logger, exception, "dispose");
        }
    }
}
