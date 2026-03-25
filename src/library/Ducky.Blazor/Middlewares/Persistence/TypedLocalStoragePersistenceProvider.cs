// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Provides persistence for state dictionaries with type preservation.
/// </summary>
public class TypedLocalStoragePersistenceProvider : IEnhancedPersistenceProvider<Dictionary<string, object>>
{
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<TypedLocalStoragePersistenceProvider> _logger;
    private readonly string _key;
    private readonly string _metadataKey;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedLocalStoragePersistenceProvider"/> class.
    /// </summary>
    public TypedLocalStoragePersistenceProvider(
        ILocalStorageService localStorage,
        PersistenceOptions options,
        ILogger<TypedLocalStoragePersistenceProvider> logger)
    {
        _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                _logger.LogDebug("No persisted state found");
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
                        _logger.LogWarning("Could not find type: {TypeName}", slice.TypeName);
                        continue;
                    }

                    // Deserialize the state with the correct type
                    object? state = JsonSerializer.Deserialize(slice.StateJson.GetRawText(), stateType, _jsonOptions);
                    if (state is not null)
                    {
                        stateDict[key] = state;
                        _logger.LogDebug("Loaded slice '{SliceKey}' with type {TypeName}", key, stateType.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize slice '{SliceKey}'", key);
                }
            }

            _logger.LogDebug("Successfully loaded {SliceCount} slices", stateDict.Count);

            return new PersistedStateContainer<Dictionary<string, object>>
            {
                State = stateDict,
                Metadata = metadata ?? new PersistenceMetadata()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load state");
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

                _logger.LogDebug("Saving slice '{SliceKey}' with type {TypeName}", key, value.GetType().Name);
            }

            // Save the dictionary and metadata
            Task dictTask = _localStorage.SetItemAsync(_key, persistedDict, cancellationToken).AsTask();
            Task metadataTask = _localStorage.SetItemAsync(_metadataKey, metadata, cancellationToken).AsTask();

            await Task.WhenAll(dictTask, metadataTask).ConfigureAwait(false);

            _logger.LogDebug("Successfully saved {SliceCount} slices", state.Count);

            return PersistenceResult.Successful();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save state");
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
            _logger.LogError(ex, "Failed to clear state");
            return PersistenceResult.Failed(ex.Message);
        }
    }

}
