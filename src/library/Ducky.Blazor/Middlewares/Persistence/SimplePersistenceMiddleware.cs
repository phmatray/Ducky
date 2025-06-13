using Blazored.LocalStorage;
using Ducky.Pipeline;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Simple persistence middleware that saves and loads state to/from local storage.
/// Each slice must handle the HydrateSliceAction to restore its state.
/// </summary>
public sealed class SimplePersistenceMiddleware : MiddlewareBase, IDisposable
{
    private readonly ILocalStorageService _localStorage;
    private readonly PersistenceOptions _options;
    private readonly string _storageKey;
    private IDispatcher? _dispatcher;
    private IStore? _store;
    private readonly Timer? _saveTimer;
    private bool _isInitialized;

    public SimplePersistenceMiddleware(
        ILocalStorageService localStorage,
        PersistenceOptions options)
    {
        _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _storageKey = "ducky:state";
        
        if (_options.DebounceDelayMs > 0)
        {
            _saveTimer = new Timer(OnSaveTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    public override async Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _dispatcher = dispatcher;
        _store = store;
        _isInitialized = true;

        if (_options.AutoHydrate)
        {
            await HydrateAsync();
        }
    }

    public async Task HydrateAsync()
    {
        if (!_options.Enabled || _dispatcher is null)
        {
            return;
        }

        try
        {
            // Load the state dictionary from local storage
            var stateDict = await _localStorage.GetItemAsync<System.Collections.Immutable.ImmutableSortedDictionary<string, object>>(_storageKey);
            
            if (stateDict != null && !stateDict.IsEmpty)
            {
                if (_options.EnableLogging)
                {
                    Console.WriteLine($"[SimplePersistence] Loading {stateDict.Count} slices from storage");
                }

                // Dispatch hydrate action for each slice
                foreach (var kvp in stateDict)
                {
                    var hydrateAction = new HydrateSliceAction(kvp.Key, kvp.Value);
                    _dispatcher.Dispatch(hydrateAction);
                    
                    if (_options.EnableLogging)
                    {
                        Console.WriteLine($"[SimplePersistence] Dispatched hydration for: {kvp.Key}");
                    }
                }
            }
            else if (_options.EnableLogging)
            {
                Console.WriteLine($"[SimplePersistence] No persisted state found");
            }
        }
        catch (Exception ex)
        {
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[SimplePersistence] Failed to load state: {ex.Message}");
            }
            
            if (_options.ErrorHandling == PersistenceErrorHandling.Throw)
            {
                throw;
            }
        }
    }

    public override void AfterReduce(object action)
    {
        if (_store is null || !_options.Enabled || !_isInitialized)
        {
            return;
        }

        // Don't persist hydration actions
        if (action is HydrateSliceAction)
        {
            return;
        }

        // Check if action should trigger persistence
        var actionType = action.GetType().Name;
        if (_options.ExcludedActionTypes.Contains(actionType))
        {
            return;
        }

        // Schedule save
        if (_options.DebounceDelayMs > 0 && _saveTimer is not null)
        {
            _saveTimer.Change(_options.DebounceDelayMs, Timeout.Infinite);
        }
        else
        {
            _ = Task.Run(() => SaveStateAsync());
        }
    }

    private void OnSaveTimerElapsed(object? state)
    {
        _ = Task.Run(() => SaveStateAsync());
    }

    private async Task SaveStateAsync()
    {
        if (_store is null || !_options.Enabled)
        {
            return;
        }

        try
        {
            var stateDict = _store.CurrentState.GetStateDictionary();
            await _localStorage.SetItemAsync(_storageKey, stateDict);

            if (_options.EnableLogging)
            {
                Console.WriteLine($"[SimplePersistence] State saved successfully");
            }
        }
        catch (Exception ex)
        {
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[SimplePersistence] Failed to save state: {ex.Message}");
            }

            if (_options.ErrorHandling == PersistenceErrorHandling.Throw)
            {
                throw;
            }
        }
    }

    public void Dispose()
    {
        _saveTimer?.Dispose();
    }
}

/// <summary>
/// Action to hydrate a specific slice with its state.
/// Each slice should handle this action to restore its state.
/// </summary>
/// <param name="SliceKey">The key identifying the slice.</param>
/// <param name="State">The state to restore (usually a JsonElement that needs deserialization).</param>
public record HydrateSliceAction(string SliceKey, object State);