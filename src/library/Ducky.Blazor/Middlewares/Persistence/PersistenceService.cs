using System.Collections.Immutable;
using Blazored.LocalStorage;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Service that handles loading and saving state to local storage.
/// </summary>
public interface IPersistenceService
{
    /// <summary>
    /// Loads the persisted state dictionary from local storage.
    /// </summary>
    Task<ImmutableSortedDictionary<string, object>?> LoadStateAsync();
    
    /// <summary>
    /// Saves the state dictionary to local storage.
    /// </summary>
    Task SaveStateAsync(ImmutableSortedDictionary<string, object> state);
}

/// <summary>
/// Implementation of persistence service using Blazored LocalStorage.
/// </summary>
public class PersistenceService : IPersistenceService
{
    private readonly ILocalStorageService _localStorage;
    private readonly string _storageKey = "ducky:state";

    public PersistenceService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
    }

    public async Task<ImmutableSortedDictionary<string, object>?> LoadStateAsync()
    {
        try
        {
            var state = await _localStorage.GetItemAsync<ImmutableSortedDictionary<string, object>>(_storageKey);
            return state;
        }
        catch
        {
            // If loading fails, return null
            return null;
        }
    }

    public async Task SaveStateAsync(ImmutableSortedDictionary<string, object> state)
    {
        try
        {
            await _localStorage.SetItemAsync(_storageKey, state);
        }
        catch
        {
            // Silently fail if saving fails
        }
    }
}

/// <summary>
/// Middleware that automatically saves state changes.
/// </summary>
public class AutoSaveMiddleware : MiddlewareBase, IDisposable
{
    private readonly IPersistenceService _persistenceService;
    private readonly PersistenceOptions _options;
    private IStore? _store;
    private readonly Timer? _saveTimer;

    public AutoSaveMiddleware(IPersistenceService persistenceService, PersistenceOptions options)
    {
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
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

    public override void AfterReduce(object action)
    {
        if (_store is null || !_options.Enabled)
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

        var stateDict = _store.CurrentState.GetStateDictionary();
        await _persistenceService.SaveStateAsync(stateDict);

        if (_options.EnableLogging)
        {
            Console.WriteLine("[AutoSave] State saved");
        }
    }

    public void Dispose()
    {
        _saveTimer?.Dispose();
    }
}