// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

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
    private readonly BehaviorSubject<IStateProvider> _stateProvider;
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
        _stateProvider = new BehaviorSubject<IStateProvider>(
            new StateSnapshot(ImmutableSortedDictionary<string, object>.Empty, []));
    }

    /// <inheritdoc />
    public override async Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _dispatcher = dispatcher;
        _store = store;

        // Initialize state provider with a snapshot of the current state
        ImmutableSortedDictionary<string, object> initStateDict = store.GetStateDictionary();
        Dictionary<Type, string> initTypeIndex = BuildTypeIndex(initStateDict);
        _stateProvider.OnNext(new StateSnapshot(initStateDict, initTypeIndex));

        // Subscribe to all effects
        foreach (ReactiveEffect effect in _effects)
        {
            try
            {
                IObservable<object> effectObservable = effect.Handle(_actions.AsObservable(), _stateProvider.AsObservable());

                IDisposable subscription = effectObservable.Subscribe(
                    onNext: action =>
                    {
                        // Dispatch the action produced by the effect
#pragma warning disable CS0618 // Reactive effects produce untyped actions – will migrate later
                        _dispatcher?.Dispatch(action);
#pragma warning restore CS0618
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

    private static Dictionary<Type, string> BuildTypeIndex(ImmutableSortedDictionary<string, object> stateDict)
    {
        Dictionary<Type, string> typeIndex = new(stateDict.Count);
        foreach ((string key, object value) in stateDict)
        {
            typeIndex.TryAdd(value.GetType(), key);
        }

        return typeIndex;
    }

    /// <inheritdoc />
    public override void AfterReduce(object action)
    {
        // Snapshot state after reduction to ensure effects see consistent state
        if (_store is not null)
        {
            ImmutableSortedDictionary<string, object> stateDict = _store.GetStateDictionary();
            Dictionary<Type, string> typeIndex = BuildTypeIndex(stateDict);
            _stateProvider.OnNext(new StateSnapshot(stateDict, typeIndex));
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
            _stateProvider.OnCompleted();
            _subscriptions.Dispose();
        });
    }
}
