namespace TheBlazorState.Storage;

/// <summary>
/// Persists state to browser sessionStorage. Survives page refresh within the same tab.
/// </summary>
public sealed class SessionStorageStrategy : IStorageStrategy
{
    internal static readonly SessionStorageStrategy Instance = new();

    private BrowserStorageService? _service;

    internal void Initialize(BrowserStorageService service) => _service = service;

    private BrowserStorageService Service =>
        _service ?? throw new InvalidOperationException(
            "SessionStorage strategy requires BrowserStorageService. Ensure AddTheBlazorState() is called.");

    public Task<StorageResult<T>> RestoreAsync<T>(string key) =>
        Service.GetAsync<T>("sessionStorage", key);

    public Task PersistAsync<T>(string key, T value, StorageMetadata metadata) =>
        Service.SetAsync("sessionStorage", key, value, metadata);

    public Task RemoveAsync(string key) =>
        Service.RemoveAsync("sessionStorage", key);
}
