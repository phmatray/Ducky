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
    private readonly SafeLogger _logger;

    public PrerenderHandoff(BlazorRegistration registration, PersistenceSlice persistence, IServiceProvider services)
    {
        (_registration, _persistence) = (registration, persistence);

        // GetService: a host without Blazor's services (a console, a test container) has nothing to take or persist.
        _storeScope = services.GetService<PersistentComponentState>();
        _logger = new((ILogger?)services.GetService<ILogger<PrerenderHandoff>>() ?? NullLogger.Instance);
        if (registration.Prerender.Count == 0)
        {
            persistence.SeedSettled.TrySetResult();
        }
    }

    // All synchronous: §6.7 starts every middleware init before awaiting any, so the restore is enqueued before the
    // component that started init renders, whoever touched the store first.
    public override ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        if (_registration.Prerender.Count == 0)
        {
            return default;
        }

        try
        {
            if (_persistence.Gate.PersistentStateOr(_storeScope) is { } state)
            {
                Take(state);

                // Always, with the explicit mode: a null-mode registration whose target is not a component throws in an
                // app with both render modes and fails the page's whole state persistence. It writes the prerender seed,
                // and the pause seed in an interactive circuit. Pausing is a circuit feature: nothing to persist in the
                // browser.
                if (!_registration.Options.IsBrowser)
                {
                    state.RegisterOnPersisting(() => PersistAsync(state), RenderMode.InteractiveAuto);
                }
            }
        }
        finally
        {
            // M6-06: in the browser a failed take waits for the first registration, bounded by PrerenderSeedWaitTimeout
            // (§11.4 browser step 2); until then it settles at once.
            _persistence.SeedSettled.TrySetResult();
        }

        return default;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "byte[] is intrinsic to STJ")]
    private void Take(PersistentComponentState state)
    {
        try
        {
            if (!state.TryTakeFromJson<byte[]>(SeedKey, out var utf8))
            {
                return;
            }

            if (JsonSerializer.Deserialize(utf8.AsSpan(), WireContext().SeedEnvelope)?.Slices is { } slices)
            {
                Store.Restore(slices, Origin.Hydration);
                return;
            }

            Log.UnreadableSeed(_logger, null);
        }
        catch (JsonException exception)
        {
            Log.UnreadableSeed(_logger, exception);
        }
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
