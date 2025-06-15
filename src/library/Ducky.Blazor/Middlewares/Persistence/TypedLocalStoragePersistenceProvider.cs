// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Provides persistence for state dictionaries with type preservation.
/// </summary>
public class TypedLocalStoragePersistenceProvider : IEnhancedPersistenceProvider<Dictionary<string, object>>
{
    private readonly ILocalStorageService _localStorage;
    private readonly string _key;
    private readonly string _metadataKey;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedLocalStoragePersistenceProvider"/> class.
    /// </summary>
    public TypedLocalStoragePersistenceProvider(
        ILocalStorageService localStorage,
        PersistenceOptions options)
    {
        _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
        _key = options.StorageKey ?? "ducky:state";
        _metadataKey = $"{_key}:metadata";

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, object>?> LoadAsync()
    {
        PersistedStateContainer<Dictionary<string, object>>? container = await LoadWithMetadataAsync().ConfigureAwait(false);
        return container?.State;
    }

    /// <inheritdoc />
    public async Task SaveAsync(Dictionary<string, object> state)
    {
        PersistenceMetadata metadata = new()
        {
            Version = 1,
            Timestamp = DateTime.UtcNow
        };

        await SaveWithMetadataAsync(state, metadata).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PersistedStateContainer<Dictionary<string, object>>?> LoadWithMetadataAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Load the persisted dictionary and metadata
            Task<PersistedStateDictionary?> dictTask = _localStorage
                .GetItemAsync<PersistedStateDictionary>(_key, cancellationToken)
                .AsTask();
            Task<PersistenceMetadata?> metadataTask = _localStorage
                .GetItemAsync<PersistenceMetadata>(_metadataKey, cancellationToken)
                .AsTask();

            await Task.WhenAll(dictTask, metadataTask).ConfigureAwait(false);

            PersistedStateDictionary? persistedDict = await dictTask.ConfigureAwait(false);
            PersistenceMetadata? metadata = await metadataTask.ConfigureAwait(false);

            if (persistedDict?.Slices is null || persistedDict.Slices.Count == 0)
            {
                Console.WriteLine("[TypedLocalStoragePersistenceProvider] No persisted state found");
                return null;
            }

            // Reconstruct the state dictionary with proper types
            Dictionary<string, object> stateDict = [];
            
            foreach ((string key, PersistedSlice slice) in persistedDict.Slices)
            {
                try
                {
                    // Get the type from the type name
                    Type? stateType = Type.GetType(slice.TypeName);
                    if (stateType is null)
                    {
                        Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Could not find type: {slice.TypeName}");
                        continue;
                    }

                    // Deserialize the state with the correct type
                    object? state = JsonSerializer.Deserialize(slice.StateJson.GetRawText(), stateType, _jsonOptions);
                    if (state is not null)
                    {
                        stateDict[key] = state;
                        Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Loaded slice '{key}' with type {stateType.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Failed to deserialize slice '{key}': {ex.Message}");
                }
            }

            Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Successfully loaded {stateDict.Count} slices");

            return new PersistedStateContainer<Dictionary<string, object>>
            {
                State = stateDict,
                Metadata = metadata ?? new PersistenceMetadata()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Failed to load state: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PersistenceResult> SaveWithMetadataAsync(
        Dictionary<string, object> state,
        PersistenceMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create persisted dictionary with type information
            PersistedStateDictionary persistedDict = new();

            foreach ((string key, object value) in state)
            {
                string typeName = value.GetType().AssemblyQualifiedName ?? value.GetType().FullName ?? "Unknown";
                JsonElement stateJson = JsonSerializer.SerializeToElement(value, _jsonOptions);

                persistedDict.Slices[key] = new PersistedSlice
                {
                    TypeName = typeName,
                    StateJson = stateJson
                };

                Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Saving slice '{key}' with type {value.GetType().Name}");
            }

            // Save the dictionary and metadata
            Task dictTask = _localStorage.SetItemAsync(_key, persistedDict, cancellationToken).AsTask();
            Task metadataTask = _localStorage.SetItemAsync(_metadataKey, metadata, cancellationToken).AsTask();

            await Task.WhenAll(dictTask, metadataTask).ConfigureAwait(false);

            Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Successfully saved {state.Count} slices");

            return PersistenceResult.Successful();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Failed to save state: {ex.Message}");
            return PersistenceResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<PersistenceResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Task stateTask = _localStorage.RemoveItemAsync(_key, cancellationToken).AsTask();
            Task metadataTask = _localStorage.RemoveItemAsync(_metadataKey, cancellationToken).AsTask();

            await Task.WhenAll(stateTask, metadataTask).ConfigureAwait(false);

            return PersistenceResult.Successful();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Failed to clear state: {ex.Message}");
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
            Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Failed to check existence: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<long> GetSizeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            PersistedStateDictionary? dict = await _localStorage
                .GetItemAsync<PersistedStateDictionary>(_key, cancellationToken)
                .ConfigureAwait(false);
            
            if (dict is null)
            {
                return 0;
            }

            string json = JsonSerializer.Serialize(dict, _jsonOptions);
            return Encoding.UTF8.GetByteCount(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Failed to get size: {ex.Message}");
            return -1;
        }
    }

    /// <inheritdoc />
    public async Task<PersistenceResult> MigrateAsync(
        int fromVersion,
        int toVersion,
        Func<Dictionary<string, object>, Dictionary<string, object>> migrationFunc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PersistedStateContainer<Dictionary<string, object>>? container = await LoadWithMetadataAsync(cancellationToken)
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
            Dictionary<string, object> migratedState = migrationFunc(container.State);

            // Update metadata
            container.Metadata.Version = toVersion;
            container.Metadata.Timestamp = DateTime.UtcNow;

            // Save migrated state
            return await SaveWithMetadataAsync(migratedState, container.Metadata, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TypedLocalStoragePersistenceProvider] Failed to migrate: {ex.Message}");
            return PersistenceResult.Failed(ex.Message);
        }
    }
}
