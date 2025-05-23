using Ducky.Pipeline;
using R3;

namespace Ducky.Middlewares.ReactiveEffect;

/// <summary>
/// Middleware that enables reactive side effects in response to actions and state changes.
/// </summary>
/// <typeparam name="TState">The type of the Redux state.</typeparam>
public sealed class ReactiveEffectMiddleware<TState> : StoreMiddleware, IDisposable
    where TState : class
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly Subject<object> _actions = new();
    private readonly BehaviorSubject<object> _state;
    private bool _disposed;
    private IDispatcher? _dispatcher;
    private IStore? _store;
    private readonly IEnumerable<ReactiveEffectGroup<TState>> _groups;
    private readonly IPipelineEventPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of <see cref="ReactiveEffectMiddleware{TState}"/>.
    /// </summary>
    /// <param name="publisher">The event publisher for dispatching events.</param>
    /// <param name="groups">The reactive effect groups to activate.</param>
    /// <param name="initialState">The initial state of the store.</param>
    public ReactiveEffectMiddleware(
        IPipelineEventPublisher publisher,
        IEnumerable<ReactiveEffectGroup<TState>> groups,
        TState initialState)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _state = new BehaviorSubject<object>(initialState ?? throw new ArgumentNullException(nameof(initialState)));
        _groups = groups;
    }

    /// <inheritdoc />
    public override async Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _store = store ?? throw new ArgumentNullException(nameof(store));

        Observable<object> actions = _actions.AsObservable();
        Observable<object> state = _state.AsObservable();

        foreach (ReactiveEffectGroup<TState> group in _groups)
        {
            foreach (Observable<object> obs in group.Activate(actions, state))
            {
                IDisposable sub = obs
                    .Where(action => action is not null)
                    .Subscribe(
                        action =>
                        {
                            try
                            {
                                _dispatcher.Dispatch(action);
                                PublishReactiveEffectDispatchedEvent(action);
                            }
                            catch (Exception ex)
                            {
                                PublishReactiveEffectErrorEvent(action, ex);
                            }
                        }
                    );
                _subscriptions.Add(sub);
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task AfterDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        // Defensive: Only push if not disposed
        if (_disposed)
        {
            return;
        }

        _actions.OnNext(context.Action!);

        // Push new state if available
        try
        {
            _state.OnNext(_store!.CurrentState);
        }
        catch (Exception ex)
        {
            PublishReactiveEffectErrorEvent(context.Action, ex);
        }

        await Task.CompletedTask.ConfigureAwait(false);
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

        foreach (IDisposable sub in _subscriptions)
        {
            sub.Dispose();
        }

        _subscriptions.Clear();
        _actions.OnCompleted();
        _actions.Dispose();
        _state.OnCompleted();
        _state.Dispose();
    }

    private void PublishReactiveEffectDispatchedEvent(object action)
    {
        _publisher.Publish(new ReactiveEffectDispatchedEventArgs(action));
    }

    private void PublishReactiveEffectErrorEvent(object? action, Exception ex)
    {
        _publisher.Publish(new ReactiveEffectErrorEventArgs(action, ex));
    }
}
