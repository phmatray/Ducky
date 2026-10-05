using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor;

// Whether the store renders interactively (SPEC §11.4).
internal enum InteractivityMode
{
    Unknown,
    Interactive,
    NonInteractive,
}

/// <summary>
/// The store's one interactivity gate (SPEC §11.4, §6.10), held by its <see cref="PersistenceSlice"/>, never a DI
/// service. The first decision wins: the first toucher's <see cref="RendererInfo.IsInteractive"/>, or one synchronous
/// probe when a built-in needs to know first. The first toucher also hands over its renderer-scope services (the S-7
/// fallback): built-ins use its <see cref="PersistentComponentState"/> and <see cref="IJSRuntime"/> from then on.
/// </summary>
internal sealed class InteractivityGate
{
    private readonly Lock _lock = new();
    private List<Action>? _callbacks = []; // null once the first registration took them
    private int _mode;
    private volatile PersistentComponentState? _persistentState;
    private volatile IJSRuntime? _jsRuntime;

    /// <summary>The first toucher's scoped services (its <c>AuthenticationStateProvider</c>, §11.6); null until then.</summary>
    public IServiceProvider? Services { get; private set; }

    /// <summary>
    /// The first-toucher hand-off (§11.2), before the toucher touches the store. Without <c>AddBlazor</c> there is no gate
    /// and nothing to inform, so the renderer is not even read.
    /// </summary>
    public static void HandOver(IServiceProvider services, Func<RendererInfo> renderer) =>
        services.GetService<PersistenceSlice>()?.Gate.Register(renderer().IsInteractive, services);

    /// <summary>
    /// Records the first registration and runs the <see cref="OnFirstRegistration"/> callbacks on this thread, outside the
    /// lock, once. Later registrations do nothing. A toucher whose services can't be resolved throws and leaves the gate
    /// unregistered, so the next toucher registers. If a callback throws (a bug, see <see cref="OnFirstRegistration"/>),
    /// the others still run and the toucher gets an <see cref="AggregateException"/> of the failures.
    /// </summary>
    public void Register(bool isInteractive, IServiceProvider services)
    {
        List<Action>? callbacks;
        lock (_lock)
        {
            callbacks = _callbacks;
            if (callbacks is null)
            {
                return;
            }

            // Resolved before the callbacks are taken: a throw here must not strand them.
            var persistentState = services.GetService<PersistentComponentState>();
            var jsRuntime = services.GetService<IJSRuntime>();
            _callbacks = null;
            Services = services;
            (_persistentState, _jsRuntime) = (persistentState, jsRuntime);
            Decide(isInteractive);
        }

        List<Exception>? failures = null;
        foreach (var callback in callbacks)
        {
            try
            {
                callback();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }

    /// <summary>
    /// Runs <paramref name="callback"/> inside the first registration, before that component renders; at once, on this
    /// thread, when it already happened. No TPL continuation is involved, so nothing depends on inlining.
    /// <paramref name="callback"/> must not throw: it runs inside the registering component's <c>Select</c> or
    /// <c>DuckyInitializer</c>, so it catches and logs its own failures (it still settles what it guards).
    /// </summary>
    public void OnFirstRegistration(Action callback)
    {
        lock (_lock)
        {
            if (_callbacks is not null)
            {
                _callbacks.Add(callback);
                return;
            }
        }

        callback();
    }

    /// <summary>
    /// What a built-in needs to know. <paramref name="isBrowser"/> (§5.1, read by the caller when it runs) is interactive;
    /// otherwise, while nothing decided, one synchronous probe: the ducky.js import, not awaited, failing with an
    /// <see cref="InvalidOperationException"/> synchronously (no Interactive Server) or as an already faulted task
    /// (prerender) is non-interactive, any other outcome interactive. <see cref="JsBridge"/> keeps the import task only
    /// while it is not faulted or cancelled.
    /// </summary>
    public InteractivityMode Resolve(bool isBrowser, JsBridge bridge)
    {
        if (isBrowser)
        {
            return InteractivityMode.Interactive;
        }

        if (Mode == InteractivityMode.Unknown)
        {
            Decide(!IsPrerendering(bridge));
        }

        return Mode;
    }

    /// <summary>The handed-over renderer instance once a component registered, <paramref name="storeScope"/>'s before.</summary>
    public PersistentComponentState? PersistentStateOr(PersistentComponentState? storeScope) => _persistentState ?? storeScope;

    /// <summary>A bridge that imports and calls through the handed-over runtime once a component registered, <paramref name="storeScope"/>'s before.</summary>
    public JsBridge CreateBridge(IJSRuntime storeScope, ILogger logger) => new(() => _jsRuntime ?? storeScope, logger);

    private InteractivityMode Mode => (InteractivityMode)Volatile.Read(ref _mode);

    private static bool IsPrerendering(JsBridge bridge)
    {
        try
        {
            // Reading Exception observes a faulted import, so it never surfaces as an unobserved task exception.
            return bridge.ImportAsync() is { IsFaulted: true, Exception.InnerException: InvalidOperationException };
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private void Decide(bool isInteractive) =>
        Interlocked.CompareExchange(ref _mode, (int)(isInteractive ? InteractivityMode.Interactive : InteractivityMode.NonInteractive), (int)InteractivityMode.Unknown);
}
