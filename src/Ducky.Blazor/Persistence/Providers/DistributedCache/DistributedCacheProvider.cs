using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor;

/// <summary>
/// <see cref="PersistStorage.Server"/> over the app's <see cref="IDistributedCache"/> (SPEC §11.6): the value is the UTF-8
/// envelope; <see cref="PersistOptions.MaxAge"/> sets <see cref="DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow"/>,
/// otherwise <see cref="BlazorOptions.ServerEntryOptions"/> applies. Every failure throws: a read fails the attempt, a write
/// or removal is retried by its writer (§11.5).
/// </summary>
/// <remarks>
/// The cache is resolved once, while the store's scope is alive, and held: the request scope marks itself disposed before it
/// disposes the store, so a lookup during the dispose flush would throw and lose every pending write (§11.5 phase 5a). A late
/// flush meets the cache itself, and a cache the container already disposed fails that key as a provider failure.
/// It is null where none is registered (the browser, a host without Server storage), where nothing calls in: Server storage is
/// inert in the browser and needs a cache elsewhere (DUCKY314).
/// </remarks>
internal sealed class DistributedCacheProvider(IServiceProvider services, BlazorOptions options)
{
    private readonly IDistributedCache _cache = services.GetService<IDistributedCache>()!;

    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        var bytes = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    // Always delivered when it returns: a cache has no disconnect to tell apart (the browser provider's false).
    public async ValueTask<bool> SetAsync(string key, string value, TimeSpan? maxAge, CancellationToken cancellationToken)
    {
        if (maxAge <= TimeSpan.Zero)
        {
            // Expired at once: a non-positive AbsoluteExpirationRelativeToNow throws, and the entry would be discarded anyway.
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            return await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }

        var entry = maxAge is null ? options.ServerEntryOptions ?? new() : new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = maxAge };
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        await _cache.SetAsync(key, Encoding.UTF8.GetBytes(value), entry, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async ValueTask<bool> RemoveAsync(string key, CancellationToken cancellationToken)
    {
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
