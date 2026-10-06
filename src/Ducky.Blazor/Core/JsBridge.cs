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
internal sealed class JsBridge(Func<IJSRuntime?> runtime, ILogger logger) : IAsyncDisposable
{
    internal const string ModulePath = "./_content/Ducky.Blazor/ducky.js";

    // JSInterop deserializes each result with System.Text.Json: what its result type must keep under trimming.
    private const DynamicallyAccessedMemberTypes JsonSerialized =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties;

    private readonly SafeLogger _logger = new(logger);
    private readonly Lock _gate = new();
    private Task<IJSObjectReference>? _module;
    private IJSRuntime? _moduleRuntime;
    private bool _disposed;

    /// <summary>A bridge over one runtime.</summary>
    public JsBridge(IJSRuntime runtime, ILogger logger)
        : this(() => runtime, logger)
    {
    }

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
            // The runtime is read at every use: the gate's hand-off switches it to the renderer's (§6.10), which imports anew.
            // ponytail: the import it replaces is dropped: a module that succeeded is not disposed, and one still pending that
            // only the probe saw is never observed, so a later fault reaches TaskScheduler.UnobservedTaskException (no crash by
            // default). Both need two different runtimes, which .NET 10 never hands over (S-7); retire the old task when one does.
            // A host without JS (a console or plain DI host) has no runtime: like UnsupportedJavaScriptRuntime, non-interactive.
            var current = runtime() ?? throw new InvalidOperationException("No IJSRuntime is registered: JS interop is unavailable in this host.");
            if (_module is not { IsFaulted: false, IsCanceled: false } || !ReferenceEquals(current, _moduleRuntime))
            {
#pragma warning disable RS0030 // justification: JsBridge is the single interop wrapper (§10, INV-23)
                _module = current.InvokeAsync<IJSObjectReference>("import", [ModulePath]).AsTask();
#pragma warning restore RS0030
                _moduleRuntime = current;
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
        Task<T>? invoke = null;
        try
        {
            // The token bounds this caller's wait only: the shared import keeps running for the others.
            var module = await ImportAsync().WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning disable RS0030 // justification: JsBridge is the single interop wrapper (§10, INV-23)
            // The token-free overload keeps JSRuntime.DefaultAsyncTimeout (one given a token, even None, switches it off),
            // so a call pending on a dropped circuit still times out (§8.1); the token bounds only this caller's wait.
            invoke = module.InvokeAsync<T>(identifier, Array.ConvertAll(args, static arg => arg.Value)).AsTask();
#pragma warning restore RS0030
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            var value = await invoke.WaitAsync(cancellationToken).ConfigureAwait(false);
            return (true, value);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or TaskCanceledException)
        {
            // INV-23: a pull the caller's token abandoned may still hand back a reference, disposed when it does.
            // ponytail: one the interop timeout dropped never reaches .NET (the runtime discards the late result); the
            // circuit's teardown frees it.
            if (invoke is Task<IJSStreamReference> pull)
            {
                _ = DisposeLateAsync(pull);
            }

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
        return module is { IsCompletedSuccessfully: true } ? DisposeQuietlyAsync(module.Result) : default;
#pragma warning restore VSTHRD103
    }

    private async ValueTask DisposeQuietlyAsync(IAsyncDisposable reference)
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

    // A pull the caller stopped waiting for: the reference it hands back, if it ever does, is disposed (INV-23).
    private async Task DisposeLateAsync(Task<IJSStreamReference> pull)
    {
        await ((Task)pull).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (pull.IsCompletedSuccessfully)
        {
#pragma warning disable VSTHRD103 // justification: Result of a task that completed successfully never blocks
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            await DisposeQuietlyAsync(pull.Result).ConfigureAwait(false);
#pragma warning restore VSTHRD103
        }
    }
}
