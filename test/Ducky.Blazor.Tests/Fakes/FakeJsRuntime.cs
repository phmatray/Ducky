using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Fakes;

/// <summary>
/// The tests' JS runtime (SPEC §17.2: hand-written fakes at I/O boundaries only). It is also the module its "import"
/// call returns, so <see cref="Calls"/> records every interop call, the import included, with every argument. As the real
/// runtime, a call honours its token (a cancelled one never crosses), only a call made without a token is subject to the
/// interop timeout (<see cref="DefaultTimeout"/>), and a null result is the default of a value type, except for storageSet and
/// storageRemove, which answer true as ducky.js does for a registered id.
/// Calls and disposals may come from several threads at once.
/// </summary>
internal sealed class FakeJsRuntime : IJSRuntime, IJSObjectReference
{
    public FakeJsRuntime() => Respond = (identifier, _) => identifier == "import" ? this : null;

    private readonly Lock _gate = new();
    private readonly List<JsCall> _calls = [];
    private int _disposed;

    /// <summary>A snapshot of the calls so far.</summary>
    public IReadOnlyList<JsCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>
    /// What a call returns: a value, or a <see cref="Task{TResult}"/> of <see cref="object"/> that decides when and how
    /// it completes. A throw fails the call synchronously.
    /// </summary>
    public Func<string, object?[], object?> Respond { get; set; }

    /// <summary>Thrown by the module's <see cref="DisposeAsync"/>, if set, after it yields (a disposal that completes later).</summary>
    public Exception? OnDispose { get; set; }

    public int Disposed => Volatile.Read(ref _disposed);

    /// <summary>
    /// Stands for JSRuntime.DefaultAsyncTimeout elapsing: once cancelled, every call through a token-free overload, pending
    /// or new, fails as <see cref="TaskCanceledException"/>. As the real runtime, a call given a token is exempt from it.
    /// </summary>
    public CancellationTokenSource DefaultTimeout { get; } = new();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, DefaultTimeout.Token, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<TValue>(cancellationToken);
        }

        lock (_gate)
        {
            _calls.Add(new(identifier, args ?? []));
        }

        return Respond(identifier, args ?? []) switch
        {
            Task<object?> completion => new(Cast<TValue>(identifier, completion.WaitAsync(cancellationToken))),
            var value => new(As<TValue>(identifier, value)),
        };
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposed);
        return OnDispose is null ? default : FailLater(OnDispose);
    }

    private static async ValueTask FailLater(Exception exception)
    {
        await Task.Yield();
        throw exception;
    }

    private static async Task<TValue> Cast<TValue>(string identifier, Task<object?> completion) => As<TValue>(identifier, await completion);

    // ducky.js answers storageSet and storageRemove true for a registered id (§11.7): a fake that leaves them unanswered
    // stands for that, not for a pruned id, which a test answers false explicitly.
    private static TValue As<TValue>(string identifier, object? value) =>
        (value ?? (identifier is "storageSet" or "storageRemove" ? true : null)) is { } answer ? (TValue)answer : default!;
}

internal sealed record JsCall(string Identifier, object?[] Args);
