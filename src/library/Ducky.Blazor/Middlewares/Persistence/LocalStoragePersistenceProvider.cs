using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Provides persistence for application state using browser localStorage.
/// </summary>
/// <typeparam name="TState">The type of the state to persist.</typeparam>
public class LocalStoragePersistenceProvider<TState> : IPersistenceProvider<TState>, IEnhancedPersistenceProvider<TState>
    where TState : class
{
    private readonly ILocalStorageService _localStorage;
    private readonly string _key;
    private readonly string _metadataKey;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStoragePersistenceProvider{TState}"/> class.
    /// </summary>
    /// <param name="localStorage">The local storage service.</param>
    /// <param name="key">The key used to store the state in localStorage. If null, a default key is used.</param>
    public LocalStoragePersistenceProvider(
        ILocalStorageService localStorage, string? key = null)
    {
        _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
        _key = key ?? $"ducky:{typeof(TState).FullName}:state";
        _metadataKey = key is null ? $"ducky:{typeof(TState).FullName}:metadata" : $"{key}:metadata";

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Asynchronously loads the persisted state from localStorage.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous load operation. The task result contains the loaded state,
    /// or <c>null</c> if no state is persisted.
    /// </returns>
    public async Task<TState?> LoadAsync()
    {
        try
        {
            return await _localStorage
                .GetItemAsync<TState>(_key)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load state from localStorage: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Asynchronously saves the specified state to localStorage.
    /// </summary>
    /// <param name="state">The state to persist.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SaveAsync(TState state)
    {
        try
        {
            await _localStorage
                .SetItemAsync(_key, state)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save state to localStorage: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PersistedStateContainer<TState>?> LoadWithMetadataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Load state and metadata in parallel
            Task<TState?> stateTask = _localStorage.GetItemAsync<TState>(_key, cancellationToken).AsTask();
            Task<PersistenceMetadata?> metadataTask = _localStorage
                .GetItemAsync<PersistenceMetadata>(_metadataKey, cancellationToken)
                .AsTask();

            await Task.WhenAll(stateTask, metadataTask).ConfigureAwait(false);

            TState? state = await stateTask.ConfigureAwait(false);
            PersistenceMetadata? metadata = await metadataTask.ConfigureAwait(false);

            if (state is null)
            {
                return null;
            }

            return new PersistedStateContainer<TState>
            {
                State = state,
                Metadata = metadata ?? new PersistenceMetadata()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load state with metadata from localStorage: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PersistenceResult> SaveWithMetadataAsync(
        TState state,
        PersistenceMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Save state and metadata in parallel
            Task stateTask = _localStorage.SetItemAsync(_key, state, cancellationToken).AsTask();
            Task metadataTask = _localStorage.SetItemAsync(_metadataKey, metadata, cancellationToken).AsTask();

            await Task.WhenAll(stateTask, metadataTask).ConfigureAwait(false);

            return PersistenceResult.Successful();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save state with metadata to localStorage: {ex.Message}");
            return PersistenceResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<PersistenceResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Remove both state and metadata
            Task stateTask = _localStorage.RemoveItemAsync(_key, cancellationToken).AsTask();
            Task metadataTask = _localStorage.RemoveItemAsync(_metadataKey, cancellationToken).AsTask();

            await Task.WhenAll(stateTask, metadataTask).ConfigureAwait(false);

            return PersistenceResult.Successful();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to clear state from localStorage: {ex.Message}");
            return PersistenceResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _localStorage.ContainKeyAsync(_key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to check if state exists in localStorage: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<long> GetSizeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            TState? state = await _localStorage.GetItemAsync<TState>(_key, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                return 0;
            }

            string json = JsonSerializer.Serialize(state, _jsonOptions);
            return Encoding.UTF8.GetByteCount(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to calculate state size in localStorage: {ex.Message}");
            return -1;
        }
    }

    /// <inheritdoc />
    public async Task<PersistenceResult> MigrateAsync(
        int fromVersion,
        int toVersion,
        Func<TState, TState> migrationFunc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PersistedStateContainer<TState>? container = await LoadWithMetadataAsync(cancellationToken)
                .ConfigureAwait(false);

            if (container?.State is null)
            {
                return PersistenceResult.Failed("No state to migrate");
            }

            if (container.Metadata.Version != fromVersion)
            {
                return PersistenceResult.Failed($"Expected version {fromVersion}, found {container.Metadata.Version}");
            }

            // Apply migration
            TState migratedState = migrationFunc(container.State);

            // Update metadata
            container.Metadata.Version = toVersion;
            container.Metadata.Timestamp = DateTime.UtcNow;
            container.Metadata.CustomMetadata["migrated_from"] = fromVersion;
            container.Metadata.CustomMetadata["migrated_at"] = DateTime.UtcNow;

            // Save migrated state
            PersistenceResult result = await SaveWithMetadataAsync(migratedState, container.Metadata, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success)
            {
                Console.WriteLine($"Successfully migrated state from version {fromVersion} to {toVersion}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to migrate state: {ex.Message}");
            return PersistenceResult.Failed(ex.Message);
        }
    }
}
