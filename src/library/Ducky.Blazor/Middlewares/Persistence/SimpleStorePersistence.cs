using System.Reflection;
using Blazored.LocalStorage;
using Ducky.Pipeline;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Simple store persistence that saves state automatically and provides restoration.
/// </summary>
public class SimpleStorePersistence : MiddlewareBase, IDisposable
{
    private readonly ILocalStorageService _localStorage;
    private readonly PersistenceOptions _options;
    private readonly string _storageKey = "ducky:state";
    private IStore? _store;
    private readonly Timer? _saveTimer;

    public SimpleStorePersistence(ILocalStorageService localStorage, PersistenceOptions options)
    {
        _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        
        if (_options.DebounceDelayMs > 0)
        {
            _saveTimer = new Timer(OnSaveTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    public override Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _store = store;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Restores the store state from local storage.
    /// This should be called after the first render in Blazor.
    /// </summary>
    public async Task RestoreStateAsync()
    {
        if (_store is null || !_options.Enabled)
        {
            return;
        }

        try
        {
            var persistedState = await _localStorage.GetItemAsync<System.Collections.Immutable.ImmutableSortedDictionary<string, object>>(_storageKey);
            
            if (persistedState != null && !persistedState.IsEmpty)
            {
                // Use reflection to update the store's internal state
                // This is similar to how Redux DevTools restores state
                var storeType = _store.GetType();
                var slicesField = storeType.GetField("_slices", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (slicesField?.GetValue(_store) is ObservableSlices observableSlices)
                {
                    // Create a new RootState with the persisted data
                    var newRootState = new RootState(persistedState);
                    
                    // Update the ObservableSlices with the new state
                    var updateMethod = observableSlices.GetType().GetMethod("UpdateFromRootState", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (updateMethod != null)
                    {
                        updateMethod.Invoke(observableSlices, new object[] { newRootState });
                        
                        if (_options.EnableLogging)
                        {
                            Console.WriteLine($"[SimpleStorePersistence] Restored {persistedState.Count} slices from storage");
                        }
                    }
                }
            }
            else if (_options.EnableLogging)
            {
                Console.WriteLine("[SimpleStorePersistence] No persisted state found");
            }
        }
        catch (Exception ex)
        {
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[SimpleStorePersistence] Failed to restore state: {ex.Message}");
            }
            
            if (_options.ErrorHandling == PersistenceErrorHandling.Throw)
            {
                throw;
            }
        }
    }

    public override void AfterReduce(object action)
    {
        if (_store is null || !_options.Enabled)
        {
            return;
        }

        // Skip persistence for certain action types
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
            _ = Task.Run(SaveStateAsync);
        }
    }

    private void OnSaveTimerElapsed(object? state)
    {
        _ = Task.Run(SaveStateAsync);
    }

    private async Task SaveStateAsync()
    {
        if (_store is null)
        {
            return;
        }

        try
        {
            var stateDict = _store.CurrentState.GetStateDictionary();
            await _localStorage.SetItemAsync(_storageKey, stateDict);

            if (_options.EnableLogging)
            {
                Console.WriteLine("[SimpleStorePersistence] State saved");
            }
        }
        catch (Exception ex)
        {
            if (_options.EnableLogging)
            {
                Console.WriteLine($"[SimpleStorePersistence] Failed to save state: {ex.Message}");
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