namespace Ducky.Reactive.Middlewares.ReactiveEffects;

/// <summary>
/// Middleware that manages reactive effects using System.Reactive observables.
/// </summary>
public sealed class ReactiveEffectMiddleware : MiddlewareBase
{
    private record NoAction;
    
    private readonly IEnumerable<ReactiveEffect> _effects;
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly Subject<object> _actions = new();
    private readonly BehaviorSubject<IRootState> _rootState;
    private readonly CompositeDisposable _subscriptions = [];
    private IDispatcher? _dispatcher;
    private IStore? _store;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveEffectMiddleware"/> class.
    /// </summary>
    /// <param name="effects">The collection of reactive effects to manage.</param>
    /// <param name="eventPublisher">The event publisher for error handling.</param>
    public ReactiveEffectMiddleware(
        IEnumerable<ReactiveEffect> effects,
        IStoreEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _effects = effects;
        _eventPublisher = eventPublisher;
        _rootState = new BehaviorSubject<IRootState>(null!);
    }

    /// <inheritdoc />
    public override async Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _dispatcher = dispatcher;
        _store = store;

        // Initialize root state
        if (store.CurrentState is not null)
        {
            _rootState.OnNext(store.CurrentState);
        }

        // Subscribe to all effects
        foreach (ReactiveEffect effect in _effects)
        {
            try
            {
                IObservable<object> effectObservable = effect.Handle(_actions.AsObservable(), _rootState.AsObservable());

                IDisposable subscription = effectObservable.Subscribe(
                    onNext: action =>
                    {
                        // Dispatch the action produced by the effect
                        _dispatcher?.Dispatch(action);
                    },
                    onError: error =>
                    {
                        // Publish error event
                        EffectErrorEventArgs errorEventArgs = new(error, effect.GetType(), new NoAction());
                        _eventPublisher.Publish(errorEventArgs);
                    });

                _subscriptions.Add(subscription);
            }
            catch (Exception ex)
            {
                // Handle initialization errors
                EffectErrorEventArgs errorEventArgs = new(ex, effect.GetType(), new NoAction());
                _eventPublisher.Publish(errorEventArgs);
            }
        }

        await base.InitializeAsync(dispatcher, store).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void AfterReduce(object action)
    {
        // Update state after reduction
        if (_store?.CurrentState is not null)
        {
            _rootState.OnNext(_store.CurrentState);
        }

        // Stream the action to all effects
        _actions.OnNext(action);
    }

    /// <inheritdoc />
    public override IDisposable BeginInternalMiddlewareChange()
    {
        // Return a disposable that completes the subjects when disposed
        return new DisposableCallback(() =>
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _actions.OnCompleted();
            _rootState.OnCompleted();
            _subscriptions.Dispose();
        });
    }
}
