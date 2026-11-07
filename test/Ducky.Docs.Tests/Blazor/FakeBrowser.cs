using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.JSInterop;

namespace Ducky.Docs.Tests.Blazor;

// The browser at the JS interop boundary (SPEC §17.2: fakes at I/O boundaries only): it is also the ducky.js module its
// "import" returns, keeps localStorage and sessionStorage in memory and records every call. Several stores may share one
// instance, as the tabs of one browser share its storage. DevTools answers as an installed extension when Extension is set.
internal sealed class FakeBrowser : IJSRuntime, IJSObjectReference
{
    private readonly ConcurrentQueue<(string Identifier, object?[] Args)> _calls = new();

    /// <summary>The stored values by (area, key): area is "local" or "session".</summary>
    public ConcurrentDictionary<(string Area, string Key), string> Storage { get; } = new();

    public bool Extension { get; init; }

    /// <summary>When set and not completed, every storageGet answers once it completes: a test sees a hydration read pending.</summary>
    public Task? ReadsHeld { get; set; }

    public IReadOnlyList<(string Identifier, object?[] Args)> Calls => [.. _calls];

    public IEnumerable<object?[]> CallsTo(string identifier) => Calls.Where(c => c.Identifier == identifier).Select(static c => c.Args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        args ??= [];
        _calls.Enqueue((identifier, args));
        return identifier == "storageGet" && ReadsHeld is { IsCompleted: false } held
            ? new(AnswerLaterAsync<TValue>(held, identifier, args))
            : new(Answer<TValue>(identifier, args));
    }

    public ValueTask DisposeAsync() => default;

    /// <summary>
    /// Delivers a storage notification as ducky.js does: to the [JSInvokable("OnStorageChanged")] method of the .NET
    /// reference the store passed to its single watchStorage call, found by that identifier as Blazor's dispatcher does.
    /// </summary>
    public Task NotifyStorageChangedAsync(string source, string area, string key, string? value)
    {
        var watch = CallsTo("watchStorage").Single();
        var target = watch[1]!.GetType().GetProperty("Value")!.GetValue(watch[1])!;
        var method = target.GetType().GetMethods().Single(m => m.GetCustomAttribute<JSInvokableAttribute>()?.Identifier == "OnStorageChanged");
        return (Task)method.Invoke(target, [source, area, key, value, false, watch[0]])!;
    }

    private async Task<TValue> AnswerLaterAsync<TValue>(Task held, string identifier, object?[] args)
    {
        await held;
        return Answer<TValue>(identifier, args);
    }

    private TValue Answer<TValue>(string identifier, object?[] args)
    {
        object? answer = identifier switch
        {
            "import" => this,
            "storageGet" => Storage.GetValueOrDefault(((string)args[0]!, (string)args[1]!)),
            "storageSet" => Storage[((string)args[0]!, (string)args[1]!)] = (string)args[2]!,
            "storageRemove" => Storage.TryRemove(((string)args[0]!, (string)args[1]!), out _) | true,
            "devtoolsConnect" => Extension,
            _ => null,
        };
        // ducky.js answers true for every bool export it is not asked about here (storageSet, watchStorage, devtoolsSend…).
        return answer is TValue value ? value : typeof(TValue) == typeof(bool) ? (TValue)(object)true : default!;
    }
}
