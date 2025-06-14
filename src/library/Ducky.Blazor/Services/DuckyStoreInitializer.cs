// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Services;

/// <summary>
/// Service responsible for initializing the Ducky store in Blazor applications.
/// </summary>
public sealed class DuckyStoreInitializer
{
    private readonly IStore _store;
    private readonly ILogger<DuckyStoreInitializer> _logger;
    private readonly TaskCompletionSource<bool> _initializationTask = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckyStoreInitializer"/> class.
    /// </summary>
    /// <param name="store">The store to initialize.</param>
    /// <param name="logger">The logger.</param>
    public DuckyStoreInitializer(IStore store, ILogger<DuckyStoreInitializer> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a task that completes when the store is initialized.
    /// </summary>
    public Task InitializationTask => _initializationTask.Task;

    /// <summary>
    /// Initializes the store asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    public async Task InitializeAsync()
    {
        if (_store.IsInitialized)
        {
            _initializationTask.TrySetResult(true);
            return;
        }

        try
        {
            _logger.LogInformation("Initializing Ducky store...");
            
            if (_store is DuckyStore duckyStore)
            {
                await duckyStore.InitializeAsync().ConfigureAwait(false);
                _logger.LogInformation("Ducky store initialized successfully.");
                _initializationTask.TrySetResult(true);
            }
            else
            {
                // Store is already initialized or doesn't require async initialization
                _logger.LogInformation("Store does not require async initialization.");
                _initializationTask.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Ducky store.");
            _initializationTask.TrySetException(ex);
            throw;
        }
    }
}
