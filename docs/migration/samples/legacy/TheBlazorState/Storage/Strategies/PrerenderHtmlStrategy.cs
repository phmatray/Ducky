namespace TheBlazorState.Storage;

internal sealed class PrerenderHtmlStrategy : IStorageStrategy
{
    internal static readonly PrerenderHtmlStrategy Instance = new();
    public Task<StorageResult<T>> RestoreAsync<T>(string key) => Task.FromResult(new StorageResult<T>(false, default, null));
    public Task PersistAsync<T>(string key, T value, StorageMetadata metadata) => Task.CompletedTask;
    public Task RemoveAsync(string key) => Task.CompletedTask;
}
