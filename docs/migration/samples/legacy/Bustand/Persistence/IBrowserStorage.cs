namespace Bustand.Persistence;

/// <summary>
/// Abstraction for browser storage operations (localStorage/sessionStorage).
/// Provides mode-aware access that works in both WASM and Server modes.
/// </summary>
/// <remarks>
/// <para>
/// In WASM mode, storage operations are direct JS calls.
/// In Server mode, operations go through SignalR to the client.
/// </para>
/// <para>
/// <b>Important:</b> Storage is NOT available during prerendering.
/// Check <see cref="IsAvailable"/> before operations that require JS interop.
/// </para>
/// </remarks>
public interface IBrowserStorage
{
    /// <summary>
    /// Gets whether browser storage is available.
    /// Returns false during prerendering when JS interop is unavailable.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Retrieves and deserializes a value from storage.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="key">The storage key.</param>
    /// <param name="storageType">Which storage to use (Local or Session).</param>
    /// <returns>The deserialized value, or default if not found or unavailable.</returns>
    Task<T?> GetAsync<T>(string key, StorageType storageType) where T : class;

    /// <summary>
    /// Serializes and stores a value in storage.
    /// </summary>
    /// <typeparam name="T">The type being stored.</typeparam>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="storageType">Which storage to use (Local or Session).</param>
    Task SetAsync<T>(string key, T value, StorageType storageType) where T : class;

    /// <summary>
    /// Removes a value from storage.
    /// </summary>
    /// <param name="key">The storage key to remove.</param>
    /// <param name="storageType">Which storage to use (Local or Session).</param>
    Task RemoveAsync(string key, StorageType storageType);

    /// <summary>
    /// Marks storage as available. Called after first render when JS interop becomes available.
    /// </summary>
    void SetAvailable();

    /// <summary>
    /// Marks storage as temporarily unavailable.
    /// Called when circuit disconnects in Blazor Server mode.
    /// </summary>
    void SetUnavailable();

    /// <summary>
    /// Event raised when storage availability changes.
    /// Subscribers can use this to trigger state re-restoration on reconnect.
    /// </summary>
    event Action? OnAvailabilityChanged;
}
