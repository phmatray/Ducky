using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky;

// The store facade (SPEC §5.2), created by DI through Create (§6.10).
internal sealed class DuckyStore : IStore
{
    // middleware and effects: create the store's middleware and effects in registration order, on its first use (§6.6); an
    // effect not Owned (an AddEffect(instance) instance) is never disposed. A lazy sequence lets a constructor's throw
    // dispose the instances built before it.
    // initTimeout, disposeTimeout and timeProvider default to DuckyBuilder's InitTimeout, DisposeTimeout and TimeProvider.System.
    // scope: the store scope a Singleton store owns (§6.10), disposed last by dispose phase 5b.
    // initBufferCapacity: the soft bound of the init buffer, DuckyBuilder.InitBufferCapacity (§6.7).
    // throwOnUnhandledAction: DuckyBuilder.ThrowOnUnhandledAction, read by the unhandled check (§6.4 step 12).
    // json: the options UseJson built (§10); none gives a store without type info.
    // disposeHook: resolves the server's StoreDisposeHook as the last step of materialization (§6.11); null otherwise.
    // meterFactory: the container's IMeterFactory, if any; without one the store records no metrics (§9).
    internal DuckyStore(
        IEnumerable<Slice> slices,
        ILogger logger,
        int maxDispatchDepth = Dispatcher.DefaultMaxDispatchDepth,
        TimeSpan? initTimeout = null,
        TimeSpan? disposeTimeout = null,
        TimeProvider? timeProvider = null,
        Func<IEnumerable<Middleware>>? middleware = null,
        Func<IEnumerable<(Effect Effect, bool Owned)>>? effects = null,
        AsyncServiceScope? scope = null,
        int initBufferCapacity = Dispatcher.DefaultInitBufferCapacity,
        bool throwOnUnhandledAction = false,
        JsonSerializerOptions? json = null,
        Func<object>? disposeHook = null,
        IMeterFactory? meterFactory = null)
    {
        Slice[] owned = [.. slices];
        foreach (var slice in owned)
        {
            slice.Freeze();
        }

        var registry = new Registry(owned);
        Slices = Array.AsReadOnly(owned);
        Scope = scope;
        InitialState = new StateSnapshot(registry);
        var safeLogger = new SafeLogger(logger);
        Json = new DuckyJson(json ?? DuckyJson.NoTypeInfo(), safeLogger);
        Dispatcher = new Dispatcher(
            registry,
            InitialState,
            safeLogger,
            new SafeTelemetry(safeLogger, meterFactory),
            maxDispatchDepth,
            throwOnUnhandledAction,
            initBufferCapacity,
            initTimeout ?? TimeSpan.FromSeconds(10),
            disposeTimeout ?? TimeSpan.FromSeconds(2),
            timeProvider ?? TimeProvider.System,
            scope,
            new(() => Build(effects, middleware, disposeHook), LazyThreadSafetyMode.ExecutionAndPublication),
            new(),
            Json);

        // Last, once the store is whole: a SliceStore dispatches through it (§12).
        foreach (var slice in owned)
        {
            slice.Attach(this);
        }
    }

    // The IStore factory AddDucky registers (§6.10): validates once per container (INV-31), then builds this store.
    internal static DuckyStore Create(IServiceProvider services, DuckyConfig config)
    {
        var logger = (ILogger?)services.GetService<ILogger<DuckyStore>>() ?? NullLogger.Instance;
        config.ThrowIfInvalid(services, logger);
        var singleton = config.Lifetime == ServiceLifetime.Singleton;
        if (singleton && !config.IsBrowser)
        {
            Log.SingletonOutsideBrowser(new SafeLogger(logger));
        }

        AsyncServiceScope? scope = singleton ? services.CreateAsyncScope() : null;
        var storeServices = scope?.ServiceProvider ?? services;
        return new DuckyStore(
            config.CreateSlices(),
            logger,
            config.MaxDispatchDepth,
            initTimeout: config.InitTimeout,
            disposeTimeout: config.DisposeTimeout,
            timeProvider: services.GetRequiredService<TimeProvider>(),
            middleware: () => config.CreateMiddleware(storeServices),
            effects: () => config.CreateEffects(storeServices),
            scope: scope,
            initBufferCapacity: config.InitBufferCapacity,
            throwOnUnhandledAction: config.ThrowOnUnhandledAction,
            json: config.Json,
            disposeHook: singleton ? null : () => storeServices.GetRequiredService<StoreDisposeHook>(),
            meterFactory: services.GetService<IMeterFactory>());
    }

    // Registry data: reading it starts nothing.
    public IReadOnlyList<Slice> Slices { get; }

    public StateSnapshot InitialState { get; }

    public DuckyJson Json { get; }

    // Materializes and starts init (one volatile read each once done). A first read sees StoreInitialized reduced only when
    // every init completes synchronously and no other drain is active; InitializeAsync gives the guarantee.
    public StateSnapshot State
    {
        get
        {
            Dispatcher.Materialize();
            Dispatcher.StartInit();
            return Dispatcher.State;
        }
    }

    internal Dispatcher Dispatcher { get; }

    // The store scope (§6.10): a Singleton store lives in the root provider, so it owns one scope that its effects and
    // middleware resolve from (disposed by dispose step 5b). Null for a Scoped store, which resolves from its own DI scope.
    internal AsyncServiceScope? Scope { get; }

    // Every entry point below materializes first, so a constructor's DUCKY353 is thrown synchronously (§6.6, §7 rule 4).
    public void Dispatch(object action)
    {
        Dispatcher.Materialize();
        Dispatcher.Dispatch(action, Origin.Local);
    }

    public Task<DispatchResult> DispatchAsync(object action)
    {
        Dispatcher.Materialize();
        return Dispatcher.DispatchAsync(action, Origin.Local);
    }

    // Select's interleaving seam (§6.8, §17.1): invoked between the add and the State read; null in production.
    internal Action? AfterSubscribeHook { get; set; }

    // §6.8: materialize, start init (it may drain inline before the subscription exists), add with last = Unset, read
    // State after the add and install last with a CAS from Unset. Without onChange nothing is subscribed: nothing runs on the drainer.
    // A selector that throws at step 3 fails Select and is unsubscribed: no Selection exists to dispose it. After
    // disposal Subscribe holds nothing and no drain runs again, so the selection is inert.
    public Selection<T> Select<T>(Func<StateSnapshot, T> selector, Action<T>? onChange = null, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        Dispatcher.Materialize();
        Dispatcher.StartInit();
        if (onChange is null)
        {
            return Selection<T>.Create(() => selector(Dispatcher.State));
        }

        var subscription = new Subscription<T>(selector, onChange, comparer ?? EqualityComparer<T>.Default);
        Dispatcher.Subscribe(subscription);
        try
        {
            AfterSubscribeHook?.Invoke();
            subscription.Install(Dispatcher.State);
        }
        catch
        {
            Dispatcher.Unsubscribe(subscription);
            throw;
        }

        return Selection<T>.Create(() => selector(Dispatcher.State), onDispose: () => Dispatcher.Unsubscribe(subscription));
    }

    // Never starts init, and never enters the init buffer (§5.2). A JsonElement is cloned here, so the caller may dispose
    // its JsonDocument as soon as this returns.
    public void Restore(IReadOnlyDictionary<string, object> values, Origin origin)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (origin is not (Origin.Hydration or Origin.CrossTab or Origin.DevTools))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Restore takes Origin.Hydration, Origin.CrossTab or Origin.DevTools.");
        }

        Dispatcher.Materialize();
        Dispatcher.Enqueue(Dispatcher.NewPending(new HydrateSlices([.. values.Select(Detach)]), origin, null));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Dispatcher.Materialize();
        return Dispatcher.InitializeAsync(cancellationToken);
    }

    // Starts init; a cancelled token ends only this caller's wait (§6.3, §6.7).
    public Task WhenIdleAsync(CancellationToken cancellationToken = default)
    {
        Dispatcher.Materialize();
        return Dispatcher.WhenIdleAsync(cancellationToken);
    }

    // Idempotent: every caller, a re-entrant one included, gets the one disposal task (§6.11).
    public ValueTask DisposeAsync() => new(Dispatcher.DisposeAsync());

    public void Dispose() => Dispatcher.Dispose();

    // A JsonElement is cloned so the caller may dispose its document (§5.2).
    private static KeyValuePair<string, object> Detach(KeyValuePair<string, object> entry) =>
        entry.Value is JsonElement element ? new(entry.Key, element.Clone()) : entry;

    // The Lazy's factory (§6.6). It runs on the first use, after the constructor assigned Dispatcher.
    private Materialized Build(
        Func<IEnumerable<(Effect Effect, bool Owned)>>? effects,
        Func<IEnumerable<Middleware>>? middleware,
        Func<object>? disposeHook) =>
        Dispatcher.Build(() => effects?.Invoke() ?? [], () => (middleware?.Invoke() ?? []).Select(Attach), disposeHook);

    // Store and DisposeTimeout are attached as each middleware is built, before any hook or init can run (§5.6). The
    // factory runs on the first use, after the constructor assigned Dispatcher, so the timeout is the clamped one step 3
    // waits with.
    private Middleware Attach(Middleware middleware)
    {
        middleware.Attach(this);
        return middleware;
    }
}
