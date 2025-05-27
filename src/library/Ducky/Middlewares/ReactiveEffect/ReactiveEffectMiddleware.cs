using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using R3;

namespace Ducky.Middlewares.ReactiveEffect;

/// <summary>
/// Middleware that enables reactive side effects in response to actions and state changes.
/// </summary>
public sealed class ReactiveEffectMiddleware : IActionMiddleware, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];
    private readonly Subject<object> _actions = new();
    private readonly BehaviorSubject<IRootState> _state;
    private readonly IReactiveEffect[] _effects;
    private readonly IDispatcher _dispatcher;
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly Func<IRootState> _getState;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="ReactiveEffectMiddleware"/>.
    /// </summary>
    /// <param name="services">The service provider used to resolve dependencies.</param>
    /// <param name="getState">A function that returns the current state of the application.</param>
    /// <param name="dispatcher">The dispatcher used to handle actions.</param>
    /// <param name="eventPublisher">The event publisher for dispatching reactive effect events.</param>
    public ReactiveEffectMiddleware(
        IServiceProvider services,
        Func<IRootState> getState,
        IDispatcher dispatcher,
        IStoreEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _getState = getState;
        _dispatcher = dispatcher;
        _eventPublisher = eventPublisher;
        
        // Initialize state with current state
        _state = new BehaviorSubject<IRootState>(_getState());
        
        // Resolve all registered reactive effects
        _effects = services.GetServices<IReactiveEffect>().ToArray();
        
        // Set up effect subscriptions
        InitializeEffects();
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
                        _eventPublisher.Publish(new ReactiveEffectErrorEventArgs(null, error));
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
                            _eventPublisher.Publish(new ReactiveEffectErrorEventArgs(action, ex));
                        }
                    });
                
                _subscriptions.Add(subscription);
            }
            catch (Exception ex)
            {
                _eventPublisher.Publish(new ReactiveEffectErrorEventArgs(null, ex));
            }
        }
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        // Pass through actions unchanged before reduce
        return actions;
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        // After reduce, emit actions to effects and update state
        return actions.Do(context =>
        {
            if (_disposed || context.IsAborted)
            {
                return;
            }

            // Emit action to effects
            _actions.OnNext(context.Action);

            // Update state after reducer has processed
            try
            {
                IRootState currentState = _getState();
                _state.OnNext(currentState);
            }
            catch (Exception ex)
            {
                _eventPublisher.Publish(new ReactiveEffectErrorEventArgs(context.Action, ex));
            }
        });
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
