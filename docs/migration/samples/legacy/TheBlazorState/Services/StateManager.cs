using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TheBlazorState.Abstractions;
using TheBlazorState.Configuration;
using TheBlazorState.Extensions;
using TheBlazorState.Storage;

namespace TheBlazorState.Services;

/// <summary>
/// Central service for managing persisted state in Blazor components.
/// Wraps <see cref="PersistentComponentState"/> with a friendlier API,
/// and uses <see cref="IMemoryCache"/> to persist state across page reloads.
///
/// Inject this into components via the generated code (do not inject manually).
/// </summary>
public sealed class StateManager(
    PersistentComponentState persistence,
    IMemoryCache cache,
    ILogger<StateManager> logger,
    TheBlazorStateOptions options,
    StorageStrategyInitializer initializer,
    CrossTabSyncService crossTabSync,
    CrossTabHub hub) : IDisposable
{
    private readonly List<PersistingComponentStateSubscription> _subscriptions = [];
    private readonly List<Func<Task>> _persistCallbacks = [];
    private readonly List<IDisposable> _hubSubscriptions = [];
    private readonly List<(string Key, IStorageStrategy Strategy)> _trackedKeys = [];
    private readonly string _circuitId = Guid.NewGuid().ToString("N");
    private bool _registered;
    private bool _disposed;

    // initializer is injected to force DI ordering; suppress CS9113
    private readonly StorageStrategyInitializer _initializerRef = initializer;

    private static readonly MemoryCacheEntryOptions CacheEntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    // Blazor's SignalR hub serializes JS interop args with camelCase (JsonSerializerDefaults.Web).
    // Cross-tab sync receives raw JSON from localStorage, so we must match that casing.
    private static readonly JsonSerializerOptions CaseInsensitiveJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Restores a property value from prerender state or server cache (synchronous path).
    /// Called by generated code during OnInitialized for [Persist] properties.
    /// The sync path (PersistentComponentState + IMemoryCache) ALWAYS runs regardless of strategy.
    /// </summary>
    /// <typeparam name="T">Type of the property value.</typeparam>
    /// <param name="key">Unique persistence key for this property.</param>
    /// <param name="strategy">The configured storage strategy (may be null for default).</param>
    /// <param name="meta">The StateMeta companion for this property.</param>
    /// <param name="valueSetter">Action to set the backing field value.</param>
    /// <param name="valueGetter">Func to get the current backing field value.</param>
    public void RestoreProperty<T>(
        string key,
        IStorageStrategy? strategy,
        StateMeta meta,
        Action<T> valueSetter,
        Func<T> valueGetter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // Always register persist callback + eager change handler (before any early returns).
        RegisterPersistPropertyCallback(key, strategy, valueGetter);
        RegisterEagerChangeHandler(key, strategy, meta, valueGetter, valueSetter);

        var ttl = meta.TimeToLive;

        // Try prerender state
        try
        {
            var wasRestored = persistence.TryTakeFromJson<PersistedEnvelope<T>>(key, out var envelope);
            if (wasRestored && envelope is not null)
            {
                if (ttl.HasValue && DateTimeOffset.UtcNow - envelope.PersistedAt > ttl.Value)
                {
                    logger.LogDebug("Property '{Key}': restored value discarded (TTL expired)", key);
                }
                else
                {
                    valueSetter(envelope.Value);
                    meta.MarkRestored(envelope.PersistedAt);
                    logger.LogDebug("Property '{Key}': restored from prerender", key);
                    return;
                }
            }

            // Try server cache
            if (cache.TryGetValue<PersistedEnvelope<T>>(key, out var cached) && cached is not null)
            {
                if (ttl.HasValue && DateTimeOffset.UtcNow - cached.PersistedAt > ttl.Value)
                {
                    logger.LogDebug("Property '{Key}': cached value discarded (TTL expired)", key);
                }
                else
                {
                    valueSetter(cached.Value);
                    meta.MarkRestored(cached.PersistedAt);
                    logger.LogDebug("Property '{Key}': restored from server cache", key);
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or InvalidCastException)
        {
            logger.LogWarning(ex, "Property '{Key}': deserialization failed, using default", key);
        }

        logger.LogDebug("Property '{Key}': no persisted value, using default", key);
    }

    private void RegisterEagerChangeHandler<T>(
        string key, IStorageStrategy? strategy, StateMeta meta, Func<T> valueGetter, Action<T> valueSetter)
    {
        var effectiveStrategy = strategy ?? options.DefaultStorage;
        _trackedKeys.Add((key, effectiveStrategy));
        var previousValue = valueGetter();

        meta.OnChanged += () =>
        {
            var value = valueGetter();
            var now = DateTimeOffset.UtcNow;

            // Always update server cache eagerly
            cache.Set(key, new PersistedEnvelope<T> { Value = value, PersistedAt = now }, CacheEntryOptions);

            // Log the change (once, in the library — no demo code needed)
            var source = meta.SuppressPersist
                ? Abstractions.ChangeSource.CrossTab
                : Abstractions.ChangeSource.Local;
            meta.LogChange(previousValue?.ToString(), value?.ToString(), source);
            previousValue = value;

            // When SuppressPersist is set (e.g. hub notification from another circuit),
            // skip writing back to browser storage and skip re-publishing to the hub.
            if (meta.SuppressPersist)
                return;

            // Write to browser strategy eagerly (fire-and-forget on circuit thread)
            if (effectiveStrategy is not PrerenderHtmlStrategy and not ServerMemoryCacheStrategy)
            {
                _ = PersistToBrowserStrategyAsync(key, value, now, effectiveStrategy, meta.TimeToLive);
            }

            // Notify other circuits via the server-side hub
            if (effectiveStrategy is LocalStorageStrategy)
            {
                var envelope = new PersistedEnvelope<T> { Value = value, PersistedAt = now };
                var json = JsonSerializer.Serialize(envelope, CaseInsensitiveJson);
                hub.Publish(key, json, _circuitId);
            }
        };

        // Subscribe to hub notifications from other circuits
        if (effectiveStrategy is LocalStorageStrategy)
        {
            var hubSub = hub.Subscribe(key, (_, rawJson) =>
            {
                // Skip if this StateManager was disposed (stale prerender subscription)
                if (_disposed) return;

                try
                {
                    var envelope = JsonSerializer.Deserialize<PersistedEnvelope<T>>(rawJson, CaseInsensitiveJson);
                    if (envelope is not null)
                    {
                        // Skip if value is already current — prevents echo-back from
                        // stale prerender subscriptions and duplicate hub notifications.
                        if (EqualityComparer<T>.Default.Equals(valueGetter(), envelope.Value))
                            return;

                        valueSetter(envelope.Value);
                        meta.MarkDirty();
                        meta.SuppressPersist = true;
                        meta.RaiseChanged();
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed data
                }
            }, subscriberId: _circuitId);
            _hubSubscriptions.Add(hubSub);

            // Also keep the JS-based cross-tab sync for Blazor WASM (no server hub)
            crossTabSync.RegisterKey(key, rawJson =>
            {
                if (_disposed) return;

                try
                {
                    var envelope = JsonSerializer.Deserialize<PersistedEnvelope<T>>(rawJson, CaseInsensitiveJson);
                    if (envelope is not null)
                    {
                        // Skip if value is already current — prevents echo-back when
                        // the hub already delivered this change, or if the JS storage
                        // event fires in the originating tab (browser quirk).
                        if (EqualityComparer<T>.Default.Equals(valueGetter(), envelope.Value))
                            return;

                        valueSetter(envelope.Value);
                        meta.MarkDirty();
                        meta.SuppressPersist = true;
                        meta.RaiseChanged();
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed data from other tabs
                }
            });
        }
    }

    /// <summary>
    /// Asynchronously restores a property value from a browser storage strategy (e.g., LocalStorage, SessionStorage, IndexedDB).
    /// Called by generated code during OnInitializedAsync for [Persist] properties.
    /// Skips if already restored or if strategy is PrerenderHtml/ServerMemoryCache (already handled sync).
    /// </summary>
    public async Task RestorePropertyAsync<T>(
        string key,
        IStorageStrategy? strategy,
        StateMeta meta,
        Action<T> valueSetter)
    {
        // Resolve effective strategy
        var effective = strategy ?? options.DefaultStorage;

        // Skip for strategies already handled by the sync RestoreProperty path
        if (effective is PrerenderHtmlStrategy or ServerMemoryCacheStrategy)
            return;

        // For browser strategies (LocalStorage, SessionStorage, IndexedDB), ALWAYS
        // attempt restore even if the sync path already restored from prerender HTML
        // or server cache. During prerender, JS interop is unavailable so the factory
        // may have overwritten user-modified data. Browser storage holds the user's
        // truth and must get the final word.
        try
        {
            var result = await effective.RestoreAsync<T>(key);
            if (result.Found && result.Value is not null)
            {
                var ttl = meta.TimeToLive;
                if (ttl.HasValue && result.PersistedAt.HasValue
                    && DateTimeOffset.UtcNow - result.PersistedAt.Value > ttl.Value)
                {
                    logger.LogDebug("Property '{Key}': async restored value discarded (TTL expired)", key);
                    return;
                }

                valueSetter(result.Value);
                meta.MarkRestored(result.PersistedAt ?? DateTimeOffset.UtcNow);
                logger.LogDebug("Property '{Key}': restored from {Strategy}", key, effective.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Property '{Key}': async restore failed, using default", key);
        }

        // Start cross-tab sync listener (idempotent — safe to call multiple times)
        await crossTabSync.StartListeningAsync();
    }

    private void RegisterPersistPropertyCallback<T>(string key, IStorageStrategy? strategy, Func<T> valueGetter)
    {
        // Resolve effective strategy
        var effective = strategy ?? options.DefaultStorage;

        RegisterPersistCallback(async () =>
        {
            var envelope = new PersistedEnvelope<T>
            {
                Value = valueGetter(),
                PersistedAt = DateTimeOffset.UtcNow
            };

            // Always write to prerender HTML + server cache
            persistence.PersistAsJson(key, envelope);
            cache.Set(key, envelope, CacheEntryOptions);

            // Additionally write to the configured strategy if it's a browser strategy.
            // Skip during prerender — JSInterop isn't available. The eager OnChanged
            // handler writes to browser storage during interactive mode instead.
            if (effective is not (PrerenderHtmlStrategy or ServerMemoryCacheStrategy))
            {
                try
                {
                    var metadata = new StorageMetadata(key, null, envelope.PersistedAt);
                    await effective.PersistAsync(key, envelope.Value, metadata);
                }
                catch (InvalidOperationException)
                {
                    // Expected during prerender — JSInterop not yet available
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Property '{Key}': failed to persist to {Strategy}", key, effective.GetType().Name);
                }
            }
        });
    }

    private async Task PersistToBrowserStrategyAsync<T>(
        string key, T value, DateTimeOffset timestamp,
        IStorageStrategy strategy, TimeSpan? ttl)
    {
        try
        {
            var metadata = new StorageMetadata(key, ttl, timestamp);
            await strategy.PersistAsync(key, value, metadata);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Property '{Key}': eager persist to {Strategy} FAILED",
                key, strategy.GetType().Name);
        }
    }

    private void RegisterPersistCallback(Func<Task> callback)
    {
        _persistCallbacks.Add(callback);

        // Register a single OnPersisting subscription that calls all callbacks.
        // We defer this to avoid race conditions during shutdown.
        if (!_registered)
        {
            _registered = true;
            _subscriptions.Add(persistence.RegisterOnPersisting(async () =>
            {
                var snapshot = _persistCallbacks.ToArray();
                foreach (var cb in snapshot)
                    await cb();
            }));
        }
    }

    /// <summary>
    /// Clears all persisted state: removes every tracked key from the server
    /// cache and from its browser storage strategy. Call this before a force
    /// reload to fully reset application state.
    /// </summary>
    public async Task ClearAllAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (var (key, strategy) in _trackedKeys)
        {
            cache.Remove(key);

            if (strategy is not PrerenderHtmlStrategy and not ServerMemoryCacheStrategy)
            {
                try
                {
                    await strategy.RemoveAsync(key);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Property '{Key}': failed to remove from {Strategy}", key, strategy.GetType().Name);
                }
            }
        }

        logger.LogDebug("ClearAllAsync: removed {Count} keys", _trackedKeys.Count);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var sub in _hubSubscriptions)
            sub.Dispose();
        _hubSubscriptions.Clear();

        foreach (var sub in _subscriptions)
            sub.Dispose();

        _subscriptions.Clear();
        _persistCallbacks.Clear();
    }

    /// <summary>
    /// Internal envelope that wraps the persisted value with metadata.
    /// Stored as JSON in the prerendered HTML.
    /// </summary>
    internal sealed class PersistedEnvelope<T>
    {
        public T Value { get; init; } = default!;
        public DateTimeOffset PersistedAt { get; init; }
    }

}
