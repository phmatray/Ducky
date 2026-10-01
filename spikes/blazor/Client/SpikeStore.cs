using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Client;

// Stands in for a Ducky store and its PrerenderHandoff/PersistenceMiddleware (SPEC §6.10, §11.4, §11.6): a singleton in
// the browser that owns a store scope, scoped on the server where the DI scope is the store scope. The first component
// touches it with its renderer-scope services, as DuckyComponent will.
public sealed class SpikeStore : IAsyncDisposable
{
    public const string SeedKey = "ducky:seed";
    private static int _next;

    private readonly AsyncServiceScope? _own;
    private readonly List<string> _auth = [];
    private AuthenticationStateProvider? _provider;
    private bool _touched;
    private bool _interactive;

    public SpikeStore(IServiceProvider services)
    {
        if (OperatingSystem.IsBrowser())
        {
            // A singleton is constructed from the root provider; WebAssemblyHost.Services is a scope (S-7 answer 1).
            SpikeInfo.Root = services;
            _own = services.CreateAsyncScope();
            StoreScope = _own.Value.ServiceProvider;
        }
        else
        {
            StoreScope = services;
        }
    }

    public event Action? Changed;

    public int Id { get; } = Interlocked.Increment(ref _next);

    public IServiceProvider StoreScope { get; }

    public int Counter { get; set; }

    public string Identity { get; private set; } = "";

    public string Probe { get; private set; } = "";

    public string Seed { get; private set; } = "";

    public string NullMode { get; private set; } = "";

    public IReadOnlyList<string> AuthEvents => _auth;

    public void Touch(IServiceProvider renderer, RendererInfo info, bool nullMode)
    {
        if (_touched)
        {
            return;
        }

        _touched = true;
        _interactive = info.IsInteractive;
        Identity = string.Join('\n',
            Same<PersistentComponentState>(renderer),
            Same<IJSRuntime>(renderer),
            Same<AuthenticationStateProvider>(renderer));
        if (SpikeInfo.HostServices is { } hs)
        {
            Identity += $"\nhost.Services: is root {ReferenceEquals(hs, SpikeInfo.Root)}; is renderer {ReferenceEquals(hs, renderer)}; "
                + $"AuthenticationStateProvider host==renderer {ReferenceEquals(Resolve<AuthenticationStateProvider>(hs), Resolve<AuthenticationStateProvider>(renderer))}";
        }

        Probe = ProbeImport();
        Seed = TakeSeed(renderer);
        var state = StoreScope.GetRequiredService<PersistentComponentState>();
        if (TryTake(state, "spike:null", out var v))
        {
            NullMode = $"null-mode key taken: {Encoding.UTF8.GetString(v)}; ";
        }

        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        state.RegisterOnPersisting(PersistSeed, RenderMode.InteractiveAuto);
        if (nullMode)
        {
            try
            {
                state.RegisterOnPersisting(PersistNullMode);
                NullMode += "RegisterOnPersisting without a render mode accepted";
            }
            catch (Exception e)
            {
                NullMode += $"RegisterOnPersisting without a render mode threw {e.GetType().Name}: {e.Message}";
            }
        }

        SubscribeAuth();
    }

    public ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            _provider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }

        SpikeInfo.Log?.Invoke($"store {Id} disposed");
        return _own?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private string Same<T>(IServiceProvider renderer)
        where T : class
    {
        var r = Resolve<T>(renderer);
        var s = Resolve<T>(StoreScope);
        var o = SpikeInfo.Root is { } root ? Resolve<T>(root) : "no root recorded";
        return $"{typeof(T).Name}: {SpikeInfo.Lifetime(typeof(T))}; renderer==store {Same(r, s)}; renderer==root {Same(r, o)}";

        static string Same(object? a, object? b) =>
            a is string ea ? ea : b is string eb ? eb : a is null || b is null ? "null" : ReferenceEquals(a, b).ToString();
    }

    private static object? Resolve<T>(IServiceProvider services)
        where T : class
    {
        try
        {
            return services.GetService<T>();
        }
        catch (Exception e)
        {
            return $"resolve threw {e.GetType().Name}";
        }
    }

    // SPEC §11.4: the gate's probe calls the import without awaiting; this records whether prerender shows as a synchronous
    // InvalidOperationException or as an already-faulted task (S-7 answer 3: both occur).
    private string ProbeImport()
    {
        var js = StoreScope.GetRequiredService<IJSRuntime>();
        try
        {
            var task = js.InvokeAsync<IJSObjectReference>("import", "./spike.js").AsTask();
            var result = $"import: no synchronous throw ({js.GetType().Name}, task {task.Status}"
                + (task.IsFaulted ? $" with {task.Exception!.InnerException!.GetType().Name}: {task.Exception.InnerException.Message})" : ")");
            _ = Observe(task);
            return result;
        }
        catch (Exception e)
        {
            return $"import: synchronous {e.GetType().Name} ({js.GetType().Name}): {e.Message}";
        }

        async Task Observe(Task<IJSObjectReference> task)
        {
            try
            {
                await using var module = await task;
                Probe += " -> completed";
            }
            catch (Exception e)
            {
                Probe += $" -> faulted {e.GetType().Name}";
            }

            Changed?.Invoke();
        }
    }

    private string TakeSeed(IServiceProvider renderer)
    {
        var store = StoreScope.GetService<PersistentComponentState>();
        if (store is not null && TryTake(store, SeedKey, out var bytes))
        {
            return $"taken through the store scope: {Encoding.UTF8.GetString(bytes)}";
        }

        var own = renderer.GetService<PersistentComponentState>();
        if (own is not null && !ReferenceEquals(own, store) && TryTake(own, SeedKey, out bytes))
        {
            return $"taken through the renderer scope only: {Encoding.UTF8.GetString(bytes)}";
        }

        return "no seed";
    }

    private Task PersistSeed()
    {
        var envelope = $"{{\"src\":\"{(_interactive ? "pause" : "prerender")}\",\"counter\":{Counter},\"store\":{Id}}}";
        Put(StoreScope.GetRequiredService<PersistentComponentState>(), SeedKey, Encoding.UTF8.GetBytes(envelope));
        SpikeInfo.Log?.Invoke($"store {Id} persisted {envelope}");
        return Task.CompletedTask;
    }

    private Task PersistNullMode()
    {
        Put(StoreScope.GetRequiredService<PersistentComponentState>(), "spike:null", Encoding.UTF8.GetBytes($"store {Id}"));
        SpikeInfo.Log?.Invoke($"store {Id} persisted the null-mode key");
        return Task.CompletedTask;
    }

// S-3: .NET 10 has no JsonTypeInfo overload and no public bytes API on PersistentComponentState, so the seed goes through
    // PersistAsJson/TryTakeFromJson<byte[]> under the audited suppression (SPEC §10). -p:SeedPath=Json drops it.
#if !SEED_JSON
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "byte[] is intrinsic to STJ")]
#endif
    private static bool TryTake(PersistentComponentState state, string key, [NotNullWhen(true)] out byte[]? bytes) =>
        state.TryTakeFromJson(key, out bytes) && bytes is not null;

#if !SEED_JSON
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "byte[] is intrinsic to STJ")]
#endif
    private static void Put(PersistentComponentState state, string key, byte[] bytes) => state.PersistAsJson(key, bytes);

    // SPEC §11.6: subscribe on the store scope's provider, then resolve the scope with DuckyScopes.NameIdentifier's shape.
    private void SubscribeAuth()
    {
        _provider = StoreScope.GetService<AuthenticationStateProvider>();
        if (_provider is null)
        {
            _auth.Add($"store {Id}: no AuthenticationStateProvider");
            return;
        }

        _provider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        Record("subscribed, epoch-0 resolution");
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task) =>
        Record($"AuthenticationStateChanged (event task completed {task.IsCompletedSuccessfully})");

    private void Record(string what)
    {
        var scope = NameIdentifier(_provider!).AsTask();
        var line = $"store {Id} provider {RuntimeHelpers.GetHashCode(_provider):x8}: {what}; NameIdentifier synchronous {scope.IsCompletedSuccessfully}"
            + (scope.IsCompletedSuccessfully ? $" = {scope.Result ?? "null"}" : "");

        _auth.Add(line);
        SpikeInfo.Log?.Invoke(line);
        Changed?.Invoke();
    }

    private static async ValueTask<string?> NameIdentifier(AuthenticationStateProvider provider) =>
        (await provider.GetAuthenticationStateAsync()).User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
