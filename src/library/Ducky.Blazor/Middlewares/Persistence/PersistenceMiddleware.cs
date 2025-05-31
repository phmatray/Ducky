using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ducky;
using Ducky.Pipeline;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Modern middleware that handles state persistence and hydration with comprehensive features.
/// </summary>
public sealed class PersistenceMiddleware : IMiddleware, IDisposable
{
    private readonly IEnhancedPersistenceProvider<IRootState> _persistenceProvider;
    private readonly HydrationManager _hydrationManager;
    private readonly PersistenceOptions _options;
    private IDispatcher? _dispatcher;
    private IStore? _store;

    private bool _isEnabled;
    private bool _isHydrated;
    private readonly object _persistenceLock = new();
    private DateTime _lastPersistenceTime = DateTime.MinValue;
    private readonly Timer _debounceTimer;
    private readonly Timer _throttleTimer;
    private string? _lastPersistedStateHash;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistenceMiddleware"/> class.
    /// </summary>
    /// <param name="persistenceProvider">The enhanced persistence provider.</param>
    /// <param name="hydrationManager">The hydration manager.</param>
    /// <param name="options">Configuration options for persistence.</param>
    public PersistenceMiddleware(
        IEnhancedPersistenceProvider<IRootState> persistenceProvider,
        HydrationManager hydrationManager,
        PersistenceOptions options)
    {
        _persistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
        _hydrationManager = hydrationManager ?? throw new ArgumentNullException(nameof(hydrationManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _isEnabled = _options.Enabled;

        // Initialize timers for debouncing and throttling
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
        _throttleTimer = new Timer(OnThrottleElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _dispatcher = dispatcher;
        _store = store;

        // Start auto-hydration if enabled
        if (_options.AutoHydrate)
        {
            return HydrateAsync();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AfterInitializeAllMiddlewares()
    {
        // Nothing to do after all middlewares are initialized
    }

    /// <summary>
    /// Performs hydration from persisted state.
    /// </summary>
    public async Task HydrateAsync()
    {
        if (!_isEnabled || _isHydrated || _dispatcher is null)
        {
            return;
        }

        string hydrationId = Guid.NewGuid().ToString();
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            _hydrationManager.StartHydrating();
            _dispatcher.Dispatch(new HydrationStartedAction("persistence", hydrationId));

            PersistedStateContainer<IRootState>? container = await LoadPersistedStateWithRetryAsync().ConfigureAwait(false);

            if (container?.State is not null)
            {
                IRootState state = container.State;

                // Apply hydration transformation if configured
                if (_options.TransformStateForHydration is not null)
                {
                    state = _options.TransformStateForHydration(state);
                }

                // Dispatch enhanced hydrate action
                _dispatcher.Dispatch(new EnhancedHydrateAction<IRootState>(
                    state,
                    container.Metadata,
                    "persistence",
                    hydrationId));

                LogIfEnabled($"Hydrated state from version {container.Metadata.Version} (persisted at {container.Metadata.Timestamp})");

                _dispatcher.Dispatch(new HydrationCompletedAction("persistence", hydrationId, true, stopwatch.Elapsed));
            }
            else
            {
                LogIfEnabled("No persisted state found for hydration");
                _dispatcher.Dispatch(new HydrationCompletedAction("persistence", hydrationId, false, stopwatch.Elapsed));
            }

            _hydrationManager.FinishHydrating();
            _isHydrated = true;

            // Replay queued actions if enabled
            if (_options.QueueActionsOnHydration)
            {
                foreach (object action in _hydrationManager.DequeueAll())
                {
                    _dispatcher.Dispatch(action);
                }
            }

            // Persist initial state if configured
            if (_options.PersistInitialState)
            {
                await PersistCurrentStateAsync("initial").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogIfEnabled($"Hydration failed: {ex.Message}");
            _dispatcher.Dispatch(new HydrationFailedAction("persistence", hydrationId, ex.Message, stopwatch.Elapsed));

            if (_options.ErrorHandling == PersistenceErrorHandling.Throw)
            {
                throw;
            }

            if (_options.ErrorHandling == PersistenceErrorHandling.LogAndDisable)
            {
                _isEnabled = false;
            }
        }
    }

    /// <inheritdoc />
    public bool MayDispatchAction(object action)
    {
        // Allow all actions
        return true;
    }

    /// <inheritdoc />
    public void BeforeDispatch(object action)
    {
        // Queue actions during hydration if enabled
        if (!_options.QueueActionsOnHydration || !_hydrationManager.IsHydrating || IsHydrationAction(action))
        {
            return;
        }

        _hydrationManager.EnqueueAction(action);
    }

    /// <inheritdoc />
    public void AfterDispatch(object action)
    {
        if (_store is null)
        {
            return;
        }

        // Skip persistence during hydration or for hydration actions
        if (!_isEnabled || _hydrationManager.IsHydrating || IsHydrationAction(action))
        {
            return;
        }

        // Check if action should trigger persistence
        if (!ShouldPersistForAction(action))
        {
            return;
        }

        // Check if state should be persisted
        if (!ShouldPersistState(_store.CurrentState))
        {
            return;
        }

        // Schedule persistence based on throttling/debouncing configuration
        SchedulePersistence("action");
    }

    /// <inheritdoc />
    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { });
    }

    /// <summary>
    /// Schedules persistence based on throttling and debouncing settings.
    /// </summary>
    /// <param name="trigger">What triggered the persistence.</param>
    private void SchedulePersistence(string trigger)
    {
        lock (_persistenceLock)
        {
            DateTime now = DateTime.UtcNow;

            // Apply throttling
            if (_options.ThrottleDelayMs > 0)
            {
                if (now - _lastPersistenceTime < TimeSpan.FromMilliseconds(_options.ThrottleDelayMs))
                {
                    return; // Skip this persistence due to throttling
                }
            }

            // Apply debouncing
            if (_options.DebounceDelayMs > 0)
            {
                _debounceTimer.Change(_options.DebounceDelayMs, Timeout.Infinite);
                return;
            }

            // Immediate persistence
            _ = Task.Run(() => PersistCurrentStateAsync(trigger));
        }
    }

    /// <summary>
    /// Called when debounce timer elapses.
    /// </summary>
    private void OnDebounceElapsed(object? state)
    {
        _ = Task.Run(() => PersistCurrentStateAsync("debounced"));
    }

    /// <summary>
    /// Called when throttle timer elapses.
    /// </summary>
    private void OnThrottleElapsed(object? state)
    {
        _ = Task.Run(() => PersistCurrentStateAsync("throttled"));
    }

    /// <summary>
    /// Persists the current state.
    /// </summary>
    /// <param name="trigger">What triggered the persistence.</param>
    private async Task PersistCurrentStateAsync(string trigger)
    {
        if (!_isEnabled || _dispatcher is null || _store is null)
        {
            return;
        }

        string persistenceId = Guid.NewGuid().ToString();
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            _dispatcher.Dispatch(new PersistenceTriggeredAction(trigger, persistenceId));

            IRootState currentState = _store.CurrentState;

            // Apply filtering based on whitelist/blacklist
            IRootState filteredState = ApplyStateFiltering(currentState);

            // Apply persistence transformation if configured
            if (_options.TransformStateForPersistence is not null)
            {
                filteredState = _options.TransformStateForPersistence(filteredState);
            }

            // Check if state has actually changed
            string stateHash = ComputeStateHash(filteredState);
            if (stateHash == _lastPersistedStateHash)
            {
                LogIfEnabled("State unchanged, skipping persistence");
                return;
            }

            // Create metadata
            PersistenceMetadata metadata = new()
            {
                Version = _options.Version,
                Timestamp = DateTime.UtcNow,
                ApplicationVersion = GetApplicationVersion(),
                UserAgent = GetUserAgent(),
                Checksum = stateHash
            };

            // Persist the state
            PersistenceResult result = await _persistenceProvider.SaveWithMetadataAsync(filteredState, metadata).ConfigureAwait(false);

            if (result.Success)
            {
                _lastPersistedStateHash = stateHash;
                _lastPersistenceTime = DateTime.UtcNow;

                long bytesApprox = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(filteredState));

                LogIfEnabled($"State persisted successfully ({bytesApprox} bytes)");
                _dispatcher.Dispatch(new PersistenceCompletedAction(persistenceId, bytesApprox, stopwatch.Elapsed));
            }
            else
            {
                LogIfEnabled($"Persistence failed: {result.Error}");
                _dispatcher.Dispatch(new PersistenceFailedAction(persistenceId, result.Error ?? "Unknown error", stopwatch.Elapsed));

                HandlePersistenceError(result.Error ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            LogIfEnabled($"Persistence error: {ex.Message}");
            _dispatcher.Dispatch(new PersistenceFailedAction(persistenceId, ex.Message, stopwatch.Elapsed));

            HandlePersistenceError(ex.Message);
        }
    }

    /// <summary>
    /// Loads persisted state with retry logic.
    /// </summary>
    private async Task<PersistedStateContainer<IRootState>?> LoadPersistedStateWithRetryAsync()
    {
        int retryCount = 0;

        while (retryCount <= _options.MaxHydrationRetries)
        {
            try
            {
                return await _persistenceProvider.LoadWithMetadataAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                retryCount++;
                LogIfEnabled($"Hydration attempt {retryCount} failed: {ex.Message}");

                if (retryCount > _options.MaxHydrationRetries)
                {
                    throw;
                }

                await Task.Delay(_options.HydrationRetryDelayMs).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <summary>
    /// Applies whitelist/blacklist filtering to state.
    /// </summary>
    private IRootState ApplyStateFiltering(IRootState state)
    {
        ImmutableSortedDictionary<string, object> stateDict = state.GetStateDictionary();
        ImmutableSortedDictionary<string, object>.Builder filteredDict = stateDict.ToBuilder();

        // Apply blacklist
        foreach (string blacklistedKey in _options.BlacklistedStateKeys)
        {
            filteredDict.Remove(blacklistedKey);
        }

        // Apply whitelist if specified
        if (_options.WhitelistedStateKeys?.Length > 0)
        {
            HashSet<string> whitelistedKeys = _options.WhitelistedStateKeys.ToHashSet();
            List<string> keysToRemove = filteredDict.Keys.Where(key => !whitelistedKeys.Contains(key)).ToList();

            foreach (string keyToRemove in keysToRemove)
            {
                filteredDict.Remove(keyToRemove);
            }
        }

        return new RootState(filteredDict.ToImmutable());
    }

    /// <summary>
    /// Computes a hash of the state for change detection.
    /// </summary>
    private static string ComputeStateHash(IRootState state)
    {
        string json = JsonSerializer.Serialize(state.GetStateDictionary());
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Determines if an action should trigger persistence.
    /// </summary>
    private bool ShouldPersistForAction(object action)
    {
        // Use custom predicate if provided
        if (_options.ShouldPersistAction is not null)
        {
            return _options.ShouldPersistAction(action);
        }

        // Check excluded action types
        string actionType = action.GetType().Name;
        return !_options.ExcludedActionTypes.Contains(actionType, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the current state should be persisted.
    /// </summary>
    private bool ShouldPersistState(IRootState state)
    {
        return _options.ShouldPersistState?.Invoke(state) ?? true;
    }

    /// <summary>
    /// Determines if an action is a hydration-related action.
    /// </summary>
    private static bool IsHydrationAction(object action)
    {
        return action is HydrateAction<IRootState> or
               EnhancedHydrateAction<IRootState> or
               HydrationStartedAction or
               HydrationCompletedAction or
               HydrationFailedAction;
    }

    /// <summary>
    /// Handles persistence errors based on configuration.
    /// </summary>
    private void HandlePersistenceError(string error)
    {
        switch (_options.ErrorHandling)
        {
            case PersistenceErrorHandling.LogAndContinue:
                // Already logged, just continue
                break;
            case PersistenceErrorHandling.LogAndDisable:
                {
                    _isEnabled = false;
                    LogIfEnabled("Persistence disabled due to error");
                    break;
                }
            case PersistenceErrorHandling.Throw:
                throw new InvalidOperationException($"Persistence failed: {error}");
        }
    }

    /// <summary>
    /// Logs a message if logging is enabled.
    /// </summary>
    private void LogIfEnabled(string message)
    {
        if (!_options.EnableLogging)
        {
            return;
        }

        Console.WriteLine($"[PersistenceMiddleware] {DateTime.UtcNow:HH:mm:ss.fff} {message}");
    }

    /// <summary>
    /// Gets the application version for metadata.
    /// </summary>
    private static string? GetApplicationVersion()
    {
        try
        {
            return typeof(PersistenceMiddleware).Assembly.GetName().Version?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the user agent for metadata.
    /// </summary>
    private static string? GetUserAgent()
    {
        try
        {
            // In Blazor WebAssembly, we could potentially get this from JSInterop
            // For now, just return a generic identifier
            return "Blazor WebAssembly";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Disposes the middleware and its resources.
    /// </summary>
    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _throttleTimer?.Dispose();
    }
}
