using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky;

// The store facade (SPEC §5.2), created by DI through Create (§6.10).
internal sealed class DuckyStore : IStore
{
    // inits: the internal stand-in for middleware init (§6.7) that Ducky.Tests uses until middleware lands (M4-03).
    internal DuckyStore(
        IEnumerable<Slice> slices,
        ILogger logger,
        int maxDispatchDepth = Dispatcher.DefaultMaxDispatchDepth,
        Func<Task>[]? inits = null)
    {
        Slice[] owned = [.. slices];
        foreach (var slice in owned)
        {
            slice.Freeze();
        }

        var registry = new Registry(owned);
        Slices = Array.AsReadOnly(owned);
        InitialState = new StateSnapshot(registry);
        Dispatcher = new Dispatcher(registry, InitialState, new SafeLogger(logger), maxDispatchDepth, inits ?? []);
    }

    // The IStore factory AddDucky registers (§6.10): validates once per container (INV-31), then builds this store.
    internal static DuckyStore Create(IServiceProvider services, DuckyConfig config)
    {
        config.ThrowIfInvalid(services);
        var logger = (ILogger?)services.GetService<ILogger<DuckyStore>>() ?? NullLogger.Instance;
        var singleton = config.Lifetime == ServiceLifetime.Singleton;
        if (singleton && !config.IsBrowser)
        {
            Log.SingletonOutsideBrowser(new SafeLogger(logger));
        }

        return new DuckyStore(config.CreateSlices(), logger, config.MaxDispatchDepth)
        {
            Scope = singleton ? services.CreateAsyncScope() : null,
        };
    }

    // Registry data: reading it starts nothing.
    public IReadOnlyList<Slice> Slices { get; }

    public StateSnapshot InitialState { get; }

    // Starts init (one volatile read once started). A first read sees StoreInitialized reduced only when every init completes
    // synchronously and no other drain is active; InitializeAsync gives the guarantee.
    public StateSnapshot State
    {
        get
        {
            Dispatcher.StartInit();
            return Dispatcher.State;
        }
    }

    internal Dispatcher Dispatcher { get; }

    // The store scope (§6.10): a Singleton store lives in the root provider, so it owns one scope that its effects and
    // middleware resolve from (disposed by dispose step 5b). Null for a Scoped store, which resolves from its own DI scope.
    internal AsyncServiceScope? Scope { get; init; }

    public void Dispatch(object action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.Enqueue(Dispatcher.NewPending(action, Origin.Local, null));
    }

    public Task<DispatchResult> DispatchAsync(object action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource<DispatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Enqueue(Dispatcher.NewPending(action, Origin.Local, completion));
        return completion.Task;
    }

    // Never starts init, and never enters the init buffer (§5.2).
    public void Restore(IReadOnlyDictionary<string, object> values, Origin origin)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (origin is not (Origin.Hydration or Origin.CrossTab or Origin.DevTools))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Restore takes Origin.Hydration, Origin.CrossTab or Origin.DevTools.");
        }

        Dispatcher.Enqueue(Dispatcher.NewPending(new HydrateSlices([.. values]), origin, null));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Dispatcher.InitializeAsync(cancellationToken);
}
