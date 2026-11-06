namespace TheBlazorState.Storage;

public static class StorageStrategy
{
    public static IStorageStrategy PrerenderHtml() => PrerenderHtmlStrategy.Instance;
    public static IStorageStrategy ServerMemoryCache() => ServerMemoryCacheStrategy.Instance;
    public static IStorageStrategy SessionStorage() => SessionStorageStrategy.Instance;
    public static IStorageStrategy LocalStorage() => LocalStorageStrategy.Instance;
    public static IStorageStrategy IndexedDb() => IndexedDbStrategy.Instance;

    public static IStorageStrategy Custom(string name)
        => new CustomStrategyReference(name);
}

internal sealed record CustomStrategyReference(string Name) : IStorageStrategy
{
    public Task<StorageResult<T>> RestoreAsync<T>(string key)
        => throw new InvalidOperationException($"Custom strategy '{Name}' must be resolved through DI.");
    public Task PersistAsync<T>(string key, T value, StorageMetadata metadata)
        => throw new InvalidOperationException($"Custom strategy '{Name}' must be resolved through DI.");
    public Task RemoveAsync(string key)
        => throw new InvalidOperationException($"Custom strategy '{Name}' must be resolved through DI.");
}
