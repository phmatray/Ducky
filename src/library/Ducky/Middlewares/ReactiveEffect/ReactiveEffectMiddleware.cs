using Ducky.Pipeline;
using R3;

namespace Ducky.Middlewares.ReactiveEffect;

/// <summary>
/// Middleware that enables reactive side effects in response to actions and state changes.
/// </summary>
public sealed class ReactiveEffectMiddleware : MiddlewareBase, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];
    private readonly Subject<object> _actions = new();
    private readonly BehaviorSubject<IRootState> _state;
    private readonly IEnumerable<IReactiveEffect> _effects;
    private IDispatcher _dispatcher = null!;
    private Func<IRootState> _getState = null!;
    private readonly IStoreEventPublisher _eventPublisher;
    private bool _disposed;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of <see cref="ReactiveEffectMiddleware"/>.
    /// </summary>
    /// <param name="effects">An enumerable collection of reactive effects that implement <see cref="IReactiveEffect"/>.</param>
    /// <param name="eventPublisher">The event publisher for dispatching reactive effect events.</param>
    public ReactiveEffectMiddleware(
        IEnumerable<IReactiveEffect> effects,
        IStoreEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _effects = effects;
        _eventPublisher = eventPublisher;

        // Initialize state - will be properly set during initialization
        System.Collections.Immutable.ImmutableSortedDictionary<string, object> emptyStateDictionary = 
            System.Collections.Immutable.ImmutableSortedDictionary<string, object>.Empty;
        _state = new BehaviorSubject<IRootState>(new RootState(emptyStateDictionary));
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_subscriptions)
        {
            if (_initialized)
            {
                return;
            }

            InitializeEffects();
            _initialized = true;
        }
    }

    private void InitializeEffects()
    {
        Observable<object> actions = _actions.AsObservable();
        Observable<IRootState> state = _state.AsObservable();

        foreach (IReactiveEffect effect in _effects)
        {
            try
            {
                Observable<object> effectObservable = effect.Handle(actions, state);

                IDisposable subscription = effectObservable
                    .Catch<object, Exception>(error =>
                    {
                        _eventPublisher.Publish(new EffectErrorEventArgs(error, effect.GetType(), null!));
                        return Observable.Empty<object>();
                    })
                    .Where(action => action is not null)
                    .Subscribe(action =>
                    {
                        try
                        {
                            _dispatcher.Dispatch(action);
                            _eventPublisher.Publish(new ReactiveEffectDispatchedEventArgs(action));
                        }
                        catch (Exception ex)
                        {
                            _eventPublisher.Publish(new EffectErrorEventArgs(ex, effect.GetType(), action));
                        }
                    });

                _subscriptions.Add(subscription);
            }
            catch (Exception ex)
            {
                _eventPublisher.Publish(new EffectErrorEventArgs(ex, effect.GetType(), null!));
            }
        }
    }

    /// <inheritdoc />
    public override Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _dispatcher = dispatcher;
        _getState = () => store.CurrentState;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override bool MayDispatchAction(object action)
    {
        return true;
    }

    /// <inheritdoc />
    public override void BeforeDispatch(object action)
    {
        EnsureInitialized();
    }

    /// <inheritdoc />
    public override void AfterDispatch(object action)
    {
        // Emit action to effects and update state after dispatch
        if (_disposed)
        {
            return;
        }

        // Ensure effects are initialized before processing
        EnsureInitialized();

        // Emit action to effects
        _actions.OnNext(action);

        // Update state after reducer has processed
        try
        {
            IRootState currentState = _getState();
            _state.OnNext(currentState);
        }
        catch (Exception ex)
        {
            _eventPublisher.Publish(new ReactiveEffectErrorEventArgs(action, ex));
        }
    }

    /// <summary>
    /// Disposes of all effect subscriptions and subject resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _subscriptions.Dispose();
        _actions.OnCompleted();
        _actions.Dispose();
        _state.OnCompleted();
        _state.Dispose();
    }
}
