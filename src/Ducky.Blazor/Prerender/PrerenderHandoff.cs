using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly TimeProvider _time;
    private readonly SafeLogger _logger;
    private int _settled; // the browser's once-flag: the first-registration callback or the timeout settles the wait

    public PrerenderHandoff(BlazorRegistration registration, PersistenceSlice persistence, IServiceProvider services)
    {
        (_registration, _persistence) = (registration, persistence);

        // GetService: a host without Blazor's services (a console, a test container) has nothing to take or persist.
        _storeScope = services.GetService<PersistentComponentState>();
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

            if (JsonSerializer.Deserialize(utf8.AsSpan(), WireContext().SeedEnvelope)?.Slices is { } slices)
            {
                Store.Restore(slices, Origin.Hydration);
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

    // The states sit two levels below the envelope root, written at the store's MaxDepth: read them back at that depth.
    private BlazorWireContext WireContext()
    {
        var maxDepth = Store.Json.Options.MaxDepth is 0 ? 64 : Store.Json.Options.MaxDepth;
        return new(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = maxDepth + 2 });
    }

    // M6-05: idle wait (PrerenderIdleTimeout, Warning 2014), src, PrerenderSeedMaxWireBytes budget, Warning 2021 for an
    // unserializable slice, Debug log for include=false. Until then this interim writer seeds whatever the store holds.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "byte[] is intrinsic to STJ")]
    private Task PersistAsync(PersistentComponentState state)
    {
        state.PersistAsJson(SeedKey, SeedEnvelope.Write(Store, _registration.Prerender));
        return Task.CompletedTask;
    }
}
