using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ducky.Blazor.Tests.DistributedCache;

// The real MemoryDistributedCache, as-is (§17.2), with every async call recorded. With `yield`, GetAsync first awaits
// Task.Yield(), as Redis effectively does: MemoryDistributedCache completes synchronously and would hide the difference
// (§11.6, the NonInteractive rule). Ducky only ever calls the async members.
internal sealed class TestCache(bool yield) : IDistributedCache
{
    private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));

    /// <summary>Every call in call order: get:{key}, set:{key} or remove:{key}.</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>The entry options of the last write of each key.</summary>
    public ConcurrentDictionary<string, DistributedCacheEntryOptions> Entries { get; } = new();

    /// <summary>The stored text of <paramref name="key"/>, read past the recording.</summary>
    public string? Stored(string key) => _inner.Get(key) is { } bytes ? Encoding.UTF8.GetString(bytes) : null;

    /// <summary>Stores <paramref name="value"/> past the recording, as another app instance would.</summary>
    public void Store(string key, string value) => _inner.Set(key, Encoding.UTF8.GetBytes(value), new());

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        Calls.Enqueue($"get:{key}");
        if (yield)
        {
            await Task.Yield();
        }

        return await _inner.GetAsync(key, token);
    }

    /// <summary>When set, every write waits for it after being recorded.</summary>
    public TaskCompletionSource? SetGate { get; init; }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Calls.Enqueue($"set:{key}");
        if (SetGate is { } gate)
        {
            await gate.Task;
        }

        Entries[key] = options;
        await _inner.SetAsync(key, value, options, token);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Calls.Enqueue($"remove:{key}");
        return _inner.RemoveAsync(key, token);
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();

    public byte[]? Get(string key) => throw new NotSupportedException();

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();

    public void Refresh(string key) => throw new NotSupportedException();

    public void Remove(string key) => throw new NotSupportedException();
}

// The signed-in user (a NameIdentifier claim on an authenticated identity), or an anonymous one when null.
internal sealed class TestAuthenticationStateProvider(string? user) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user is null
        ? new ClaimsPrincipal(new ClaimsIdentity())
        : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], "test"))));
}
