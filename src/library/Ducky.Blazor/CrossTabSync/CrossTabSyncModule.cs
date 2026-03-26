// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Blazor.Middlewares.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor.CrossTabSync;

/// <summary>
/// Enables automatic Redux state synchronization across browser tabs using localStorage events.
/// </summary>
public class CrossTabSyncModule : JsModule
{
    private readonly DotNetObjectReference<CrossTabSyncModule> _objRef;
    private readonly IEnhancedPersistenceProvider<Dictionary<string, object>> _persistenceProvider;
    private readonly IDispatcher _dispatcher;
    private readonly CrossTabSyncOptions _options;
    private readonly ILogger<CrossTabSyncModule> _logger;
    private Timer? _debounceTimer;

    /// <summary>
    /// Constructs a new instance for cross-tab sync.
    /// </summary>
    /// <param name="js">Blazor JS runtime.</param>
    /// <param name="persistenceProvider">The persistence provider to load state from.</param>
    /// <param name="dispatcher">The dispatcher to dispatch hydration actions.</param>
    /// <param name="options">Cross-tab sync configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public CrossTabSyncModule(
        IJSRuntime js,
        IEnhancedPersistenceProvider<Dictionary<string, object>> persistenceProvider,
        IDispatcher dispatcher,
        CrossTabSyncOptions options,
        ILogger<CrossTabSyncModule> logger)
        : base(js, "./_content/Ducky.Blazor/crosstabSync.js")
    {
        _persistenceProvider = persistenceProvider;
        _dispatcher = dispatcher;
        _options = options;
        _logger = logger;
        _objRef = DotNetObjectReference.Create(this);
    }

    /// <summary>
    /// Starts listening for cross-tab state changes.
    /// </summary>
    public async Task StartAsync()
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("[CrossTabSync] Sync is disabled, not starting listener");
            return;
        }

        await InvokeVoidAsync(JavaScriptMethods.AddReduxStorageListener, _objRef, _options.StorageKey).ConfigureAwait(false);
    }

    /// <summary>
    /// Invoked by JS when localStorage is updated in another tab.
    /// </summary>
    [JSInvokable]
    public Task OnExternalStateChangedAsync(string? changedKey)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        // Only react to changes on our storage key
        if (changedKey is not null && changedKey != _options.StorageKey)
        {
            return Task.CompletedTask;
        }

        // Cancel previous debounce timer and start a new one
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(
            OnDebounceElapsed,
            null,
            _options.DebounceMs,
            Timeout.Infinite);

        return Task.CompletedTask;
    }

    private void OnDebounceElapsed(object? state)
    {
        _ = Task.Run(HydrateFromExternalChangeAsync);
    }

    private async Task HydrateFromExternalChangeAsync()
    {
        try
        {
            _logger.LogDebug("[CrossTabSync] Loading state from external tab change");

            PersistedStateContainer<Dictionary<string, object>>? container =
                await _persistenceProvider.LoadWithMetadataAsync().ConfigureAwait(false);

            if (container?.State is null)
            {
                _logger.LogDebug("[CrossTabSync] No persisted state found");
                return;
            }

            Dictionary<string, object> stateDict = container.State;
            int hydratedCount = 0;

            foreach ((string sliceKey, object sliceState) in stateDict)
            {
                // Filter by IncludedSliceKeys if set
                if (_options.IncludedSliceKeys is not null
                    && !_options.IncludedSliceKeys.Contains(sliceKey, StringComparer.Ordinal))
                {
                    continue;
                }

#pragma warning disable CS0618 // Using obsolete Dispatch(object) because HydrateSliceAction does not implement IAction
                _dispatcher.Dispatch(new HydrateSliceAction(sliceKey, sliceState));
#pragma warning restore CS0618
                hydratedCount++;
            }

            _logger.LogDebug("[CrossTabSync] Hydrated {Count} slices from external change", hydratedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CrossTabSync] Failed to hydrate from external change");
        }
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        _debounceTimer?.Dispose();
        _objRef.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Holds JavaScript export names.
    /// </summary>
    private static class JavaScriptMethods
    {
        public const string AddReduxStorageListener = "addReduxStorageListener";
    }
}
