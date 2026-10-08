using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace Ducky.Blazor;

// The prerender handoff (SPEC §11.4), registered by AddBlazor ahead of persistence and inert without Prerender<T> slices.
// It takes ducky:seed through the gate's handed-over PersistentComponentState once a component registered, and through
// the store scope's before that.
internal sealed class PrerenderHandoff : Middleware
{
    internal const string SeedKey = "ducky:seed";

    private readonly BlazorRegistration _registration;
    private readonly PersistenceSlice _persistence;
    private readonly PersistentComponentState? _storeScope;
    private readonly IJSRuntime? _jsRuntime;
    private readonly TimeProvider _time;
    private readonly SafeLogger _logger;
    private int _settled; // the browser's once-flag: the first-registration callback or the timeout settles the wait

    // The seed, written once per store: a page whose app configures both render modes persists through one store per
    // payload, and the framework calls the registration once per payload.
    private Task<byte[]?>? _seed;

    public PrerenderHandoff(BlazorRegistration registration, PersistenceSlice persistence, IServiceProvider services)
    {
        (_registration, _persistence) = (registration, persistence);

        // GetService: a host without Blazor's services (a console, a test container) has nothing to take or persist.
        _storeScope = services.GetService<PersistentComponentState>();
        _jsRuntime = services.GetService<IJSRuntime>();
        _time = services.GetRequiredService<TimeProvider>();
        _logger = new((ILogger?)services.GetService<ILogger<PrerenderHandoff>>() ?? NullLogger.Instance);
        if (registration.Prerender.Count == 0)
        {
            persistence.SeedSettled.TrySetResult();
        }
    }

    // The synchronous prefix takes the seed: §6.7 starts every middleware init before awaiting any, so the restore is
    // enqueued before the component that started init renders, whoever touched the store first.
    public override ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        if (_registration.Prerender.Count == 0)
        {
            return default;
        }

        var state = _persistence.Gate.PersistentStateOr(_storeScope);
        if (_registration.Options.IsBrowser)
        {
            return InitializeInBrowserAsync(state, cancellationToken);
        }

        try
        {
            Take(state);

            // Always, with the explicit mode: a null-mode registration whose target is not a component throws in an app
            // with both render modes and fails the page's whole state persistence. It writes the prerender seed, and the
            // pause seed in an interactive circuit.
            state?.RegisterOnPersisting(() => PersistAsync(state), RenderMode.InteractiveAuto);
        }
        finally
        {
            _persistence.SeedSettled.TrySetResult();
        }

        return default;
    }

    // §11.4 in the browser: WASM restores PersistentComponentState in RunAsync, so a Program.cs preload may start init
    // before the seed exists. A failed take waits for the first registration, bounded by PrerenderSeedWaitTimeout. Nothing
    // is persisted: pausing is a circuit feature.
    private ValueTask InitializeInBrowserAsync(PersistentComponentState? state, CancellationToken initToken)
    {
        // Like the circuit path, a throw from the synchronous prefix still settles: persistence must never stall on it.
        var waiting = false;
        try
        {
            if (Take(state))
            {
                return default;
            }

            // Runs inside the first component's Select, before it renders (at once when one already registered). WASM has
            // restored by then, so a failed take there means there is no seed.
            _persistence.Gate.OnFirstRegistration(() => Settle(timedOut: false, initToken));
            waiting = true;
            return new(WaitForSeedAsync(initToken));
        }
        finally
        {
            if (!waiting)
            {
                _persistence.SeedSettled.TrySetResult();
            }
        }
    }

    // The init token ends the wait at once (an overflow abort or a dispose), so dispose's init wait is never held for the
    // bound; that cancellation is not a failure, and the settle below then skips the restore. The settle sits in a finally:
    // an invalid bound (a negative TimeSpan other than infinite) makes WaitAsync throw, which fails init (logged) but
    // still settles.
    private async Task WaitForSeedAsync(CancellationToken initToken)
    {
        try
        {
            await _persistence.SeedSettled.Task.WaitAsync(_registration.Options.PrerenderSeedWaitTimeout, _time, initToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        finally
        {
            Settle(timedOut: true, initToken);
        }
    }

    // The first of the callback and the timeout settles; the other does nothing. After an abort the store is already
    // Ready: a restore would land after StoreInitialized and the replayed actions, so it is skipped (WASM is single-threaded,
    // so this check can't interleave with the abort). Later seeds (enhanced navigation, new islands) are never read.
    private void Settle(bool timedOut, CancellationToken initToken)
    {
        if (Interlocked.Exchange(ref _settled, 1) != 0)
        {
            return;
        }

        try
        {
            if (initToken.IsCancellationRequested)
            {
                Log.SeedSkippedAfterAbort(_logger);
            }
            else if (!Take(_persistence.Gate.PersistentStateOr(_storeScope)) && timedOut)
            {
                Log.SeedWaitTimedOut(_logger, _registration.Options.PrerenderSeedWaitTimeout);
            }
        }
        finally
        {
            _persistence.SeedSettled.TrySetResult();
        }
    }

    // False when nothing was persisted (the prerender pass itself, or WASM before RunAsync); an unreadable seed is taken.
    // TryTakeFromJson stays inside the catch: it throws JsonException on a value that is not a
    // JSON byte[] (a foreign or hand-edited payload), which is an unreadable seed too.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "byte[] is intrinsic to STJ")]
    private bool Take(PersistentComponentState? state)
    {
        if (state is null)
        {
            return false;
        }

        try
        {
            if (!state.TryTakeFromJson<byte[]>(SeedKey, out var utf8))
            {
                return false;
            }

            if (JsonSerializer.Deserialize(utf8.AsSpan(), WireContext().SeedEnvelope) is { Slices: { } slices } seed)
            {
                Restore(seed, slices);
            }
            else
            {
                Log.UnreadableSeed(_logger, null);
            }
        }
        catch (JsonException exception)
        {
            Log.UnreadableSeed(_logger, exception);
        }

        return true;
    }

    // §11.4: a persisted key whose recorded version differs from its PersistOptions.Version, or is missing, never comes from
    // a seed (Debug 2015): a seed can outlive a deploy, and only storage goes through EnvelopeReader's migrations. The
    // unscoped browser-storage keys a pause seed lists as dirty and restores are handed to persistence before SeedSettled
    // completes: their storage restore is skipped and their writers rewrite storage after the terminal (§11.5 step 6). A
    // dirty value that does not deserialize is no value the seed restored: left out (its Warning logged here), so storage
    // applies and is never overwritten with the initial state. Scoped keys (every key once Scope is set) keep prerender
    // precedence until the scope-hash hand-off (M12-07): a circuit resumed under another user never writes the seed's
    // value under that user's key.
    private void Restore(SeedEnvelope seed, Dictionary<string, object> slices)
    {
        List<string> dirty = [];
        foreach (var slice in Store.Slices.Where(slice => slices.ContainsKey(slice.Key)))
        {
            if (_registration.Persist.GetValueOrDefault(slice.GetType())?.Options is not { } options)
            {
                continue;
            }

            if (!(seed.Ver.TryGetValue(slice.Key, out var version) && version == options.Version))
            {
                slices.Remove(slice.Key);
                Log.SeedKeyVersionSkewed(_logger, slice.Key);
            }
            else if (_registration.Options.Scope is null && options.Storage is not PersistStorage.Server && seed.Dirty.Contains(slice.Key))
            {
                if (!Store.Json.TryDeserialize((JsonElement)slices[slice.Key], slice.StateType, out var state, slice.Key))
                {
                    slices.Remove(slice.Key);
                }
                else if (state is not null)
                {
                    slices[slice.Key] = state;
                    dirty.Add(slice.Key);
                }
            }
        }

        _persistence.SeedDirty = dirty;
        Store.Restore(slices, Origin.Hydration);
    }

    // The states sit two levels below the envelope root, written at the store's MaxDepth: read them back at that depth.
    private BlazorWireContext WireContext()
    {
        var maxDepth = Store.Json.Options.MaxDepth is 0 ? 64 : Store.Json.Options.MaxDepth;
        // RespectNullableAnnotations: a null ver or dirty is an unreadable seed (JsonException), never a null reference.
        return new(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = maxDepth + 2, RespectNullableAnnotations = true });
    }

    // The seed is persisted as PersistAsJson<byte[]> of the UTF-8 envelope, never as a string (§11.4 wire estimate).
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "byte[] is intrinsic to STJ")]
    private async Task PersistAsync(PersistentComponentState state)
    {
        _seed ??= WriteSeedAsync();

        // Back on the renderer's context: the framework reads what its callbacks persist once they all completed.
        // Stryker disable once Boolean : no SynchronizationContext is captured outside a renderer, as in these tests
        if (await _seed.ConfigureAwait(true) is { } seed)
        {
            state.PersistAsJson(SeedKey, seed);
        }
    }

    // §11.4 OnPersisting: only from an idle store, waited for at most PrerenderIdleTimeout (LongRunning effects and
    // Ducky.Reactive never count as running), else nothing and Warning 2014; then one snapshot. The circuit store's
    // callback runs when .NET pauses the circuit: a pause seed. It never depends on an Unknown gate: decided after the idle
    // wait, by one probe if no component recorded its renderer. Any failure only costs the seed (Warning 2012): a faulted
    // callback would fail the page's whole state persistence, other PersistentComponentState users included.
    private async Task<byte[]?> WriteSeedAsync()
    {
        try
        {
            var timeout = _registration.Options.PrerenderIdleTimeout;
            using (var idle = new CancellationTokenSource(timeout, _time))
            {
                try
                {
                    await Store.WhenIdleAsync(idle.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Log.SeedIdleTimeout(_logger, timeout);
                    return null;
                }
            }

            // PersistentComponentState comes with Blazor's services, IJSRuntime included.
            string src;
            var bridge = _persistence.Gate.CreateBridge(_jsRuntime!, _logger);
            await using (bridge.ConfigureAwait(false))
            {
                src = _persistence.Gate.Resolve(_registration.Options.IsBrowser, bridge) == InteractivityMode.Interactive ? "pause" : "prerender";
            }

            return SeedWriter.Write(Store, _registration, src, _persistence.Middleware!, _logger);
        }
#pragma warning disable CA1031 // justification: the seed is best effort, the page's other persisted state is not (§11.4)
        catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
        {
            Log.SeedWriteFailed(_logger, exception);
            return null;
        }
    }
}
