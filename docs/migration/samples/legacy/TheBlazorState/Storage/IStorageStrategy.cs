namespace TheBlazorState.Storage;

/// <summary>
/// Abstraction for persisting and restoring state values.
/// Implement this interface to add custom storage backends (Redis, SQLite, etc.).
/// </summary>
public interface IStorageStrategy
{
    Task<StorageResult<T>> RestoreAsync<T>(string key);
    Task PersistAsync<T>(string key, T value, StorageMetadata metadata);
    Task RemoveAsync(string key);
}
