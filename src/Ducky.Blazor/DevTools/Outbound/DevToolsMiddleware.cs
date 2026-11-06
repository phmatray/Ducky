using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace Ducky.Blazor;

// SPEC §11.8 (DT-01, DT-02), outbound: registered by UseDevTools after AddBlazor's middleware. Init connects the store's
// id (§11.7) to the extension through ducky.js; AfterReduce, on the drainer, sends each processed action with the state
// it produced as two pre-serialized strings (INV-23), both sanitized (INV-28).
internal sealed partial class DevToolsMiddleware : Middleware
{
    private readonly BlazorOptions _blazor;
    private readonly DevToolsOptions _options;
    private readonly PersistenceSlice _persistence;
    private readonly JsBridge _bridge;
    private readonly TimeProvider _time;
    private readonly SafeLogger _log;
    private readonly DotNetObjectReference<DevToolsMiddleware> _reference;
    // 2 once connected (@@INIT pending), 1 once the drainer issued @@INIT; back to 0, once, when @@INIT or a send fails or
    // names an id ducky.js doesn't know
    private int _connected;
    private volatile bool _registered; // devtoolsConnect was issued; cleared once ducky.js answers that it holds no such id

    public DevToolsMiddleware(BlazorRegistration registration, PersistenceSlice persistence, IServiceProvider services)
    {
        (_blazor, _options, _persistence) = (registration.Options, registration.DevTools!, persistence);
        var logger = (ILogger?)services.GetService<ILogger<DevToolsMiddleware>>() ?? NullLogger.Instance;
        _log = new(logger);

        // The store scope's runtime until a component hands over its renderer's (§6.10), as for persistence.
        _bridge = persistence.Gate.CreateBridge(services.GetService<IJSRuntime>(), logger);
        _time = services.GetRequiredService<TimeProvider>();
        _reference = DotNetObjectReference.Create(this);
    }

    /// <summary>The DevTools-local counter (§11.8): +1 per devtoolsSend, never <see cref="ActionContext.Id"/>.</summary>
    internal long Sends { get; private set; }

    // The gate is read in the synchronous prefix, probing once when Unknown, never awaiting a component registration
    // (§11.8): a store first touched by non-component code would otherwise hold init until InitTimeout.
    public override ValueTask InitializeAsync(CancellationToken cancellationToken) =>
        _persistence.Gate.Resolve(_blazor.IsBrowser, _bridge) == InteractivityMode.Interactive ? new(ConnectAsync(cancellationToken)) : default;

    // Drainer only. Frames are captured right here, so the first one is this hook.
    public override void AfterReduce(ActionContext context)
    {
        // @@INIT is the state before the first action reduced once connected, issued on the drainer just before that
        // action's send, so no reduce falls between them (a restore during init, §6 "Hooks versus init"). ConnectAsync may
        // write 2 concurrently (§6), so a fresh read decides the send only after this call saw 2 and issued @@INIT: a later
        // 2 can no longer arrive, and only a failed @@INIT (0) can. A second unconditional read could send before @@INIT.
        var connected = Volatile.Read(ref _connected);
        if (connected == 2)
        {
            var init = StateJson(context.PreviousState); // a throwing sanitizer leaves @@INIT pending
            Record(0, context.PreviousState);
            Volatile.Write(ref _connected, 1);
            _ = InitAsync(init);
            connected = Volatile.Read(ref _connected); // 0 when @@INIT already failed
        }

        // A DevTools restore (a jump, a reset, an import) is never sent: the extension already shows it, and a send would
        // move its index off the history's (§11.8).
        if (connected == 0 || context.Origin == Origin.DevTools || _options.Filter?.Invoke(context) == false)
        {
            return;
        }

        var frames = _options.Trace ? new StackTrace(0, false).GetFrames() : null;
        var action = ActionJson(context, frames);
        var state = StateJson(context.State);
        Sends++;
        Record(Sends, context.State);

        // Issued on the drainer, so sends reach ducky.js in reduce order; observed, never awaited.
        _ = SendAsync(action, state);
    }

    // Disconnects whenever devtoolsConnect was issued, even if init failed or .NET gave up on the connect: ducky.js may
    // hold the id and the reference.
    public override async ValueTask DisposeAsync()
    {
        if (_registered)
        {
            try
            {
                // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                await _bridge.TryInvokeAsync<IJSVoidResult>("devtoolsDisconnect", CancellationToken.None, _persistence.Id).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // justification: a disposal never throws (§6.11); ducky.js prunes the id after the prune cap
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Log.DevToolsDisconnectFailed(_log, exception, _persistence.Id);
            }
        }

        _reference.Dispose();
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        await _bridge.DisposeAsync().ConfigureAwait(false);
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        await base.DisposeAsync().ConfigureAwait(false);
    }

    // Bounded by HydrationTimeout, below InitTimeout (DUCKY316), so DevTools can never trip the init timeout. Absent
    // extension, timeout, interruption or rejection: inert, with a Warning; a disposal abandons the connect silently.
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(_blazor.HydrationTimeout, _time);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var config = JsonSerializer.Serialize(new DevToolsConfig(_options.Name, _options.MaxAge, Trace: false, _blazor.InlinePayloadBytes), BlazorWireContext.Default.DevToolsConfig);
        var id = _persistence.Id;
        Exception? failure = null;
        _registered = true;
        try
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            var (delivered, available) = await _bridge.TryInvokeAsync<bool>(
                "devtoolsConnect", bound.Token, id, JsArg.Ref(_reference), config,
                (long)PersistenceMiddleware.CrossTabSync.DeliveryPruneAfter.TotalMilliseconds).ConfigureAwait(false);
            if (delivered && !available)
            {
                _registered = false;
                Log.DevToolsExtensionAbsent(_log);
                return;
            }

            if (delivered)
            {
                Volatile.Write(ref _connected, 2); // AfterReduce issues @@INIT
                return;
            }
        }
#pragma warning disable CA1031 // justification: a rejected connect leaves DevTools inert, never fails init (§11.8)
        catch (Exception exception)
#pragma warning restore CA1031
        {
            failure = exception;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            Log.DevToolsConnectFailed(_log, failure, _blazor.HydrationTimeout);
        }
    }

    // Any failure leaves DevTools inert: the extension may or may not hold a failed send, so the counter could no longer
    // match its index (§11.8). Several sends may be in flight; only the first to fail logs.
    private async Task SendAsync(string action, string state)
    {
        Exception? failure = null;
        try
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            var (delivered, known) = await _bridge.TryInvokeAsync<bool>("devtoolsSend", CancellationToken.None, _persistence.Id, action, state).ConfigureAwait(false);
            if (known)
            {
                return;
            }

            if (delivered)
            {
                _registered = false;
                if (Interlocked.Exchange(ref _connected, 0) == 1)
                {
                    Log.DevToolsUnknownId(_log, _persistence.Id);
                }

                return;
            }
        }
#pragma warning disable CA1031 // justification: an issued, unawaited call; every fault is logged, none is left unobserved
        catch (Exception exception)
#pragma warning restore CA1031
        {
            failure = exception;
        }

        GoInert(failure);
    }

    private async Task InitAsync(string state)
    {
        Exception? failure = null;
        try
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            if ((await _bridge.TryInvokeAsync<IJSVoidResult>("devtoolsInit", CancellationToken.None, _persistence.Id, state).ConfigureAwait(false)).Delivered)
            {
                return;
            }
        }
#pragma warning disable CA1031 // justification: an issued, unawaited call; every fault is logged, none is left unobserved
        catch (Exception exception)
#pragma warning restore CA1031
        {
            failure = exception;
        }

        GoInert(failure);
    }

    // The first failure of @@INIT or of a send makes DevTools inert, with one Warning.
    private void GoInert(Exception? failure)
    {
        if (Interlocked.Exchange(ref _connected, 0) == 1)
        {
            Log.DevToolsSendFailed(_log, failure);
        }
    }

    // The FSA {type, payload, meta?, stack?}. A payload that can't be serialized is null with meta.unserializable (DuckyJson
    // logs the type once); the sanitizer still sees it.
    private string ActionJson(ActionContext context, StackFrame[]? frames)
    {
        var type = context.ActionType;
        var payload = Payload(context.Action);
        var fsa = new JsonObject { ["type"] = type, ["payload"] = _options.ActionSanitizer is { } sanitize ? Own(sanitize(type, payload)) : payload };
        if (payload is null)
        {
            fsa["meta"] = new JsonObject { ["unserializable"] = true };
        }

        if (frames is not null)
        {
            fsa["stack"] = string.Join('\n', frames.Take(_options.TraceLimit).Select(frame =>
                _options.DescribeFrame(frame) is { } method ? $"    at {method.DeclaringTypeName}.{method.Name}" : "    at <unknown>"));
        }

        return fsa.ToJsonString();
    }

    // Library actions through BlazorWireContext, which the app's resolver doesn't know (the failures projected, §10); the
    // rest through the app's resolver, by runtime type.
    private JsonNode? Payload(object action) => action switch
    {
        ReducerFailed failed => JsonSerializer.SerializeToNode(
            new ReducerFailure(failed.Exception.GetType().Name, failed.Exception.Message, failed.SliceKey), BlazorWireContext.Default.ReducerFailure),
        EffectFailed failed => JsonSerializer.SerializeToNode(
            new EffectFailure(failed.Exception.GetType().Name, failed.Exception.Message, failed.EffectType), BlazorWireContext.Default.EffectFailure),
        StoreInitialized or HydrationCompleted or HydrationFailed or PersistenceFailed or ClearPersistedState =>
            JsonSerializer.SerializeToNode(action, BlazorWireContext.Default.GetTypeInfo(action.GetType())!),
        _ => Store.Json.ToNode(action, action.GetType()),
    };

    // Each slice by its declared StateType from Store.Slices (never the runtime type, which would drop a polymorphic
    // state's discriminator), the library slice through BlazorWireContext; every one sanitized.
    // ponytail: @ducky/persistence is the only library slice; key the branch on the @ducky/ prefix when another package adds one.
    private string StateJson(StateSnapshot snapshot)
    {
        var state = new JsonObject();
        foreach (var slice in Store.Slices)
        {
            var value = snapshot.Get(slice.Key);
            var node = slice is PersistenceSlice
                ? JsonSerializer.SerializeToNode((PersistenceState)value, BlazorWireContext.Default.PersistenceState)
                : Store.Json.ToNode(value, slice.StateType);
            state[slice.Key] = _options.StateSanitizer is { } sanitize ? Own(sanitize(slice.Key, node)) : node;
        }

        return state.ToJsonString();
    }

    // A JsonNode has one parent: a sanitizer's result that is attached already (a child of its input, a cached node it
    // returned before) is copied, so no sanitizer shape can throw on the send path.
    private static JsonNode? Own(JsonNode? node) => node?.Parent is null ? node : node.DeepClone();
}
