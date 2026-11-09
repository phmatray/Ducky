using System.Collections.Concurrent;
using Microsoft.JSInterop;

namespace DuckShop.Tests;

// The browser's localStorage, in memory: Ducky.Blazor reaches storage through JS interop, so
// this stands in for the browser at that boundary. It is also the ducky.js module its
// "import" returns. Two stores sharing one instance are a page and the same page reloaded.
internal sealed class FakeBrowserStorage : IJSRuntime, IJSObjectReference
{
    // The stored entries by (area, key), area being "local" or "session".
    public ConcurrentDictionary<(string Area, string Key), string> Entries { get; } = new();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        args ??= [];
        object? answer = identifier switch
        {
            "import" => this,
            "storageGet" => Entries.GetValueOrDefault(Entry(args)),
            "storageSet" => Entries[Entry(args)] = (string)args[2]!,
            "storageRemove" => Entries.TryRemove(Entry(args), out _),
            _ => null,
        };

        // ducky.js answers true for the other calls that return a bool (watchStorage…).
        return new(answer is TValue value
            ? value
            : typeof(TValue) == typeof(bool) ? (TValue)(object)true : default!);
    }

    public ValueTask DisposeAsync() => default;

    private static (string Area, string Key) Entry(object?[] args) => ((string)args[0]!, (string)args[1]!);
}
