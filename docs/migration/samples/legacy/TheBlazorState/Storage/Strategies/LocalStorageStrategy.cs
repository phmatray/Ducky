namespace TheBlazorState.Storage;

/// <summary>
/// Persists state to browser localStorage. Survives browser restarts.
/// </summary>
public sealed class LocalStorageStrategy : IStorageStrategy
{
    internal static readonly LocalStorageStrategy Instance = new();

    private BrowserStorageService? _service;

    internal void Initialize(BrowserStorageService service) => _service = service;

    private BrowserStorageService Service =>
        _service ?? throw new InvalidOperationException(
            "LocalStorage strategy requires BrowserStorageService. Ensure AddTheBlazorState() is called.");

    public Task<StorageResult<T>> RestoreAsync<T>(string key) =>
        Service.GetAsync<T>("localStorage", key);

    public Task PersistAsync<T>(string key, T value, StorageMetadata metadata) =>
        Service.SetAsync("localStorage", key, value, metadata);

    public Task RemoveAsync(string key) =>
        Service.RemoveAsync("localStorage", key);
}
