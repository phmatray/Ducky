using Microsoft.Extensions.Caching.Memory;

namespace TheBlazorState.Storage;

/// <summary>
/// Persists state in server-side IMemoryCache. Survives page reloads on Blazor Server.
/// </summary>
public sealed class ServerMemoryCacheStrategy : IStorageStrategy
{
    internal static readonly ServerMemoryCacheStrategy Instance = new();

    private IMemoryCache? _cache;

    private static readonly MemoryCacheEntryOptions CacheEntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    internal void Initialize(IMemoryCache cache) => _cache = cache;

    private IMemoryCache Cache =>
        _cache ?? throw new InvalidOperationException(
            "ServerMemoryCache strategy requires IMemoryCache. Ensure AddTheBlazorState() is called.");

    public Task<StorageResult<T>> RestoreAsync<T>(string key)
    {
        if (Cache.TryGetValue<BrowserStorageService.StorageEnvelope<T>>(key, out var envelope)
            && envelope is not null)
        {
            return Task.FromResult(new StorageResult<T>(true, envelope.Value, envelope.PersistedAt));
        }
        return Task.FromResult(new StorageResult<T>(false, default, null));
    }

    public Task PersistAsync<T>(string key, T value, StorageMetadata metadata)
    {
        var envelope = new BrowserStorageService.StorageEnvelope<T>
        {
            Value = value,
            PersistedAt = metadata.Timestamp
        };
        Cache.Set(key, envelope, CacheEntryOptions);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        Cache.Remove(key);
        return Task.CompletedTask;
    }
}
