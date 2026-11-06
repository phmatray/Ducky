using System.Diagnostics;
using Bustand.Configuration;
using Bustand.Middleware;

namespace Bustand.Persistence;

/// <summary>
/// Middleware that persists store state to browser storage after changes.
/// Uses debounced writes to batch rapid state changes.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
/// <remarks>
/// <para>
/// This middleware does NOT block state changes in <see cref="OnBeforeChange"/>.
/// State is persisted asynchronously after the change via <see cref="OnAfterChange"/>.
/// </para>
/// <para>
/// <b>Debouncing:</b> State changes are debounced (default 300ms). Multiple rapid
/// changes result in a single storage write with the final state.
/// </para>
/// <para>
/// <b>Disposal:</b> This middleware implements IDisposable. It is disposed automatically
/// when the store is disposed (for Scoped stores) or when the app shuts down.
/// </para>
/// </remarks>
public class PersistenceMiddleware<TState> : IMiddleware<TState>, IDisposable
    where TState : class
{
    private readonly IBrowserStorage _storage;
    private readonly string _storageKey;
    private readonly StorageType _storageType;
    private readonly DebouncedWriter<TState> _writer;
    private bool _disposed;

    /// <summary>
    /// Creates a new persistence middleware instance.
    /// </summary>
    /// <param name="storage">The browser storage service.</param>
    /// <param name="storageKey">The storage key for this store.</param>
    /// <param name="storageType">Local or Session storage.</param>
    /// <param name="debounceMs">Debounce delay in milliseconds.</param>
    public PersistenceMiddleware(
        IBrowserStorage storage,
        string storageKey,
        StorageType storageType,
        int debounceMs = 300)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _storageKey = storageKey ?? throw new ArgumentNullException(nameof(storageKey));
        _storageType = storageType;

        _writer = new DebouncedWriter<TState>(
            async state => await WriteToStorageAsync(state),
            debounceMs);
    }

    /// <summary>
    /// Gets the storage key used by this middleware.
    /// </summary>
    internal string StorageKey => _storageKey;

    /// <summary>
    /// Gets the storage type used by this middleware.
    /// </summary>
    internal StorageType StorageType => _storageType;

    /// <inheritdoc />
    /// <remarks>
    /// Persistence middleware never blocks state changes.
    /// </remarks>
    public bool OnBeforeChange(MiddlewareContext<TState> context)
    {
        // Persistence middleware doesn't validate - always allow changes
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Queues the new state for debounced persistence.
    /// </remarks>
    public void OnAfterChange(MiddlewareContext<TState> context)
    {
        if (_disposed)
            return;

        // Queue the new state for debounced write
        _writer.QueueWrite(context.NewState);
    }

    /// <summary>
    /// Restores state from storage.
    /// Called during store initialization to restore persisted state.
    /// </summary>
    /// <returns>The restored state, or null if not found or unavailable.</returns>
    public async Task<TState?> RestoreStateAsync()
    {
        if (!_storage.IsAvailable)
        {
            Debug.WriteLine($"[Bustand] Storage not available for restore of '{_storageKey}'");
            return null;
        }

        try
        {
            var state = await _storage.GetAsync<TState>(_storageKey, _storageType);
            if (state is not null)
            {
                Debug.WriteLine($"[Bustand] Restored state for '{_storageKey}'");
            }
            return state;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Bustand] Failed to restore state for '{_storageKey}': {ex.Message}. Using InitialState.");
            return null;
        }
    }

    /// <summary>
    /// Forces any pending state to be written immediately.
    /// Call before disposal to ensure data is not lost.
    /// </summary>
    public async Task FlushAsync()
    {
        if (!_disposed)
        {
            await _writer.FlushAsync();
        }
    }

    private async Task WriteToStorageAsync(TState state)
    {
        if (!_storage.IsAvailable)
        {
            Debug.WriteLine($"[Bustand] Storage not available for write of '{_storageKey}'");
            return;
        }

        await _storage.SetAsync(_storageKey, state, _storageType);
        Debug.WriteLine($"[Bustand] Persisted state for '{_storageKey}'");
    }

    /// <summary>
    /// Disposes the middleware and its debounced writer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _writer.Dispose();
    }
}
