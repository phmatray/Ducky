// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Default implementation of persistence operations for application state.
/// </summary>
public class PersistenceService : IPersistenceService
{
    private readonly PersistenceMiddleware? _persistenceMiddleware;
    private readonly IEnhancedPersistenceProvider<IStateProvider>? _persistenceProvider;
    private readonly ILogger<PersistenceService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistenceService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public PersistenceService(IServiceProvider serviceProvider, ILogger<PersistenceService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Try to get PersistenceMiddleware from DI container
        _persistenceMiddleware = serviceProvider.GetService(typeof(PersistenceMiddleware)) as PersistenceMiddleware;
        _persistenceProvider = serviceProvider.GetService(
            typeof(IEnhancedPersistenceProvider<IStateProvider>)) as IEnhancedPersistenceProvider<IStateProvider>;
    }

    /// <inheritdoc />
    public bool IsHydrated => _persistenceMiddleware?.IsHydrated ?? false;

    /// <inheritdoc />
    public bool IsEnabled => _persistenceMiddleware?.IsEnabled ?? false;

    /// <inheritdoc />
    public async Task HydrateAsync()
    {
        if (_persistenceMiddleware is null)
        {
            _logger.LogWarning("PersistenceMiddleware not found. Ensure persistence is enabled in your configuration.");
            return;
        }

        try
        {
            _logger.LogInformation("Starting state hydration...");
            await _persistenceMiddleware.HydrateAsync().ConfigureAwait(false);
            _logger.LogInformation("State hydration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate state from persistence.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PersistAsync()
    {
        if (_persistenceMiddleware is null)
        {
            _logger.LogWarning("PersistenceMiddleware not found. Ensure persistence is enabled in your configuration.");
            return;
        }

        try
        {
            _logger.LogInformation("Persisting current state...");
            await _persistenceMiddleware.PersistCurrentStateAsync("manual").ConfigureAwait(false);
            _logger.LogInformation("State persisted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist state.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        if (_persistenceProvider is null)
        {
            _logger.LogWarning("PersistenceProvider not found. Ensure persistence is enabled in your configuration.");
            return;
        }

        try
        {
            _logger.LogInformation("Clearing persisted state...");
            await _persistenceProvider.ClearAsync().ConfigureAwait(false);
            _logger.LogInformation("Persisted state cleared successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear persisted state.");
            throw;
        }
    }
}
