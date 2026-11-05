using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;

namespace Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;

// The signed-in user, changed the way a circuit or MSAL does: the provider notifies its subscribers.
internal sealed class SwitchingAuth(string? user) : AuthenticationStateProvider
{
    /// <summary>When set, the next resolutions wait for it, as a slow provider does.</summary>
    public Task? Gate { get; set; }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var resolved = new AuthenticationState(user is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], "test")));
        if (Gate is { } gate)
        {
            await gate;
        }

        return resolved;
    }

    /// <summary>The handlers subscribed to the base class's field-like <c>AuthenticationStateChanged</c> event.</summary>
    public int Subscribers => (typeof(AuthenticationStateProvider)
        .GetField(nameof(AuthenticationStateChanged), BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(this) as Delegate)?.GetInvocationList().Length ?? 0;

    /// <summary>Changes the user (or re-asserts the same one) and raises <c>AuthenticationStateChanged</c> on this thread.</summary>
    public void Change(string? next)
    {
        user = next;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}

// Registered before AddBlazor: runs its plan once, on the drainer, when a reset has committed and before persistence's own
// AfterReduce has seen it.
internal sealed class HoldReset(HoldResetPlan plan) : Middleware
{
    private bool _ran;

    public override void AfterReduce(ActionContext context)
    {
        if (!_ran && context.Origin == Origin.Hydration && context.State.Get<PersistenceState>().ScopeEpoch > 0)
        {
            _ran = true;
            plan.Run();
        }
    }
}

internal sealed record HoldResetPlan(Action Run);

// The real cache through TestCache's recording, with each call held by a gate or failed on demand: call is get:{key} or
// set:{key}, consumed by the next such call, in the order they were asked for. A held call honours its token.
internal sealed class GatedCache : IDistributedCache, IDisposable
{
    // Only so a failed test never hangs in dispose. Longer than WriterHarness.Until's 10 s, or a broken abandonment would
    // fault the held call just inside that bound and pass by racing the two timers.
    private static readonly TimeSpan _safetyBound = TimeSpan.FromSeconds(30);

    private readonly TestCache _inner = new(yield: false);
    private readonly ConcurrentDictionary<string, ManualResetEventSlim> _began = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<Func<CancellationToken, Task>>> _plans = new();

    public ConcurrentQueue<string> Calls => _inner.Calls;

    public string? Stored(string key) => _inner.Stored(key);

    public void Store(string key, string value) => _inner.Store(key, value);

    /// <summary>Holds the next <paramref name="call"/> until the returned gate completes.</summary>
    public TaskCompletionSource Hold(string call)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Plan(call, token => gate.Task.WaitAsync(_safetyBound, token));
        return gate;
    }

    /// <summary>Holds the next <paramref name="call"/> until its token ends it, and nothing else does.</summary>
    public void HoldUntilCancelled(string call) => Plan(call, static token => new TaskCompletionSource().Task.WaitAsync(token));

    /// <summary>Holds the next <paramref name="call"/> until the returned gate completes, whatever its token says: a provider that ignores it.</summary>
    public TaskCompletionSource HoldIgnoringToken(string call)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Plan(call, _ => gate.Task.WaitAsync(_safetyBound, CancellationToken.None));
        return gate;
    }

    /// <summary>Fails the next <paramref name="call"/>, as a transient provider error does.</summary>
    public void Fail(string call) => Plan(call, static _ => Task.FromException(new TimeoutException("transient")));

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        await RunPlanAsync($"get:{key}", token);
        return await _inner.GetAsync(key, token);
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        await RunPlanAsync($"set:{key}", token);
        await _inner.SetAsync(key, value, options, token);
    }

    public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);

    public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();

    public byte[]? Get(string key) => throw new NotSupportedException();

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();

    public void Refresh(string key) => throw new NotSupportedException();

    public void Remove(string key) => throw new NotSupportedException();

    /// <summary>Set when <paramref name="call"/> begins, for a test blocking a drain (so it can't await).</summary>
    public ManualResetEventSlim GetBegan(string call) => _began.GetOrAdd(call, static _ => new());

    private void Plan(string call, Func<CancellationToken, Task> step) => _plans.GetOrAdd(call, static _ => new()).Enqueue(step);

    // Noted before it is held or failed (past TestCache's recording, which only sees what gets through).
    private Task RunPlanAsync(string call, CancellationToken token)
    {
        Attempts.Enqueue(call);
        GetBegan(call).Set();
        return _plans.TryGetValue(call, out var plan) && plan.TryDequeue(out var step) ? step(token) : Task.CompletedTask;
    }

    /// <summary>Every get and set in the order it began, a held or failed one included.</summary>
    public ConcurrentQueue<string> Attempts { get; } = new();

    public void Dispose() => _inner.Dispose();
}
