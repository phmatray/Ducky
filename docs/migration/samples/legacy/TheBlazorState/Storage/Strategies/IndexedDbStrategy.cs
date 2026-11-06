namespace TheBlazorState.Storage;

/// <summary>
/// Persists state to browser IndexedDB. Survives browser restarts. Suitable for large data.
/// </summary>
public sealed class IndexedDbStrategy : IStorageStrategy
{
    internal static readonly IndexedDbStrategy Instance = new();

    private BrowserStorageService? _service;

    internal void Initialize(BrowserStorageService service) => _service = service;

    private BrowserStorageService Service =>
        _service ?? throw new InvalidOperationException(
            "IndexedDb strategy requires BrowserStorageService. Ensure AddTheBlazorState() is called.");

    public Task<StorageResult<T>> RestoreAsync<T>(string key) =>
        Service.GetAsync<T>("indexedDb", key);

    public Task PersistAsync<T>(string key, T value, StorageMetadata metadata) =>
        Service.SetAsync("indexedDb", key, value, metadata);

    public Task RemoveAsync(string key) =>
        Service.RemoveAsync("indexedDb", key);
}
