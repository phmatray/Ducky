using Ducky.Middlewares;
using Ducky.Pipeline;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Middleware that handles state persistence and hydration using an IPersistenceProvider.
/// </summary>
/// <typeparam name="TState">The type of the state to persist.</typeparam>
public sealed class PersistenceMiddleware<TState> : StoreMiddleware
    where TState : class
{
    private readonly IPersistenceProvider<TState> _persistenceProvider;
    private readonly HydrationManager _hydrationManager;
    private IStore? _store;
    private bool _hydrated;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistenceMiddleware{TState}"/> class.
    /// </summary>
    /// <param name="persistenceProvider">The persistence provider to use for saving and loading state.</param>
    /// <param name="hydrationManager">The hydration manager to use for managing hydration state.</param>
    public PersistenceMiddleware(
        IPersistenceProvider<TState> persistenceProvider,
        HydrationManager hydrationManager)
    {
        _persistenceProvider = persistenceProvider;
        _hydrationManager = hydrationManager;
    }

    /// <inheritdoc />
    public override async Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _store = store;
        _hydrationManager.StartHydrating();

        TState? persistedState = await _persistenceProvider
            .LoadAsync()
            .ConfigureAwait(false);

        if (persistedState is not null)
        {
            // Dispatch a Hydrate action (user must handle this in reducer)
            dispatcher.Dispatch(new HydrateAction<TState>(persistedState));
        }

        _hydrationManager.FinishHydrating();
        _hydrated = true;

        // Replay any queued actions
        foreach (object action in _hydrationManager.DequeueAll())
        {
            dispatcher.Dispatch(action);
        }
    }

    /// <inheritdoc />
    public override StoreMiddlewareAsyncMode AsyncMode => StoreMiddlewareAsyncMode.FireAndForget;

    /// <inheritdoc />
    public override Task BeforeDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        // If hydrating, queue actions except HydrateAction
        if (!_hydrated && context.Action is not HydrateAction<TState>)
        {
            _hydrationManager.EnqueueAction(context.Action!);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task AfterDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        // Save state after every action except HydrateAction
        if (!_hydrated || context.Action is HydrateAction<TState> || _store is null)
        {
            return;
        }

        if (_store.CurrentState is not TState state)
        {
            return;
        }

        await _persistenceProvider
            .SaveAsync(state)
            .ConfigureAwait(false);
    }
}
