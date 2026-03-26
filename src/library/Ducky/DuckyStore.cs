// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Ducky.Pipeline;
using Microsoft.Extensions.Logging;

namespace Ducky;

/// <summary>
/// Represents a store that manages application state and handles actions.
/// </summary>
public sealed class DuckyStore : IStore, IDisposable
{
    private readonly IDispatcher _dispatcher;
    private readonly ActionPipeline _pipeline;
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly ILogger<DuckyStore> _logger;
    private readonly ObservableSlices _slices = new();
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly object _syncRoot = new();
    private readonly Queue<object> _reentrantQueue = [];

    private const int MaxReentrantDepth = 10;

    private bool _isDisposed;
    private bool _isDispatching;
    private int _dispatchingThreadId;
    private object? _currentAction;
    private List<string> _sliceKeys = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckyStore"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher used to enqueue actions.</param>
    /// <param name="pipeline">The reactive action pipeline that processes actions.</param>
    /// <param name="eventPublisher">The event publisher for store events.</param>
    /// <param name="slices">The initial collection of slices to register.</param>
    /// <param name="logger">The logger for store diagnostics.</param>
    public DuckyStore(
        IDispatcher dispatcher,
        ActionPipeline pipeline,
        IStoreEventPublisher eventPublisher,
        IEnumerable<ISlice> slices,
        ILogger<DuckyStore> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(slices);
        ArgumentNullException.ThrowIfNull(logger);

        _dispatcher = dispatcher;
        _pipeline = pipeline;
        _eventPublisher = eventPublisher;
        _logger = logger;

        List<string> sliceKeys = [];

        // Register slices
        foreach (ISlice slice in slices)
        {
            _slices.AddSlice(slice);
            string sliceKey = slice.GetKey();
            sliceKeys.Add(sliceKey);
            _eventPublisher.Publish(new SliceAddedEventArgs(sliceKey, slice.GetType()));
        }

        // Subscribe to slice state changes
        _slices.SliceStateChanged += OnSliceStateChanged;

        // Store the slice keys for later use during initialization
        _sliceKeys = sliceKeys;

        // For Blazor WebAssembly compatibility, we defer pipeline initialization
        // The store will be initialized via InitializeAsync method
    }

    /// <summary>
    /// Initializes the store asynchronously. This method must be called after construction.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        // Initialize the pipeline
        await _pipeline.InitializeAsync(_dispatcher, this).ConfigureAwait(false);

        // Subscribe to action stream and process through pipeline
        _dispatcher.ActionDispatched += OnActionDispatched;

        // Dispatch initial action
#pragma warning disable CS0618 // Internal action uses untyped Dispatch – will migrate later
        _dispatcher.Dispatch(new StoreInitialized());
#pragma warning restore CS0618

        // Publish store initialized event
        _eventPublisher.Publish(new StoreInitializedEventArgs(_sliceKeys.Count, _sliceKeys));

        // Mark as initialized
        IsInitialized = true;
    }

    /// <inheritdoc/>
    public event EventHandler<StateChangedEventArgs>? StateChanged;


    /// <inheritdoc/>
    public bool IsInitialized { get; private set; }

    /// <inheritdoc/>
    public DateTime StartTime => _startTime;

    /// <inheritdoc/>
    public int SliceCount => _slices.Count;

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        TimeSpan uptime = DateTime.UtcNow - _startTime;
        _eventPublisher.Publish(new StoreDisposingEventArgs(uptime));

        _dispatcher.ActionDispatched -= OnActionDispatched;
        _slices.SliceStateChanged -= OnSliceStateChanged;
        _pipeline.Dispose();
        _slices.Dispose();

        lock (_syncRoot)
        {
            _reentrantQueue.Clear();
        }
    }

    private void ProcessActionSafely(object action)
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                _logger.LogWarning(
                    "Action {ActionType} dispatched after store disposal — ignoring.",
                    action.GetType().Name);
                return;
            }

            if (_isDispatching)
            {
                // Same thread = true re-entrancy (e.g. middleware dispatching during reduce)
                if (_dispatchingThreadId == Environment.CurrentManagedThreadId)
                {
                    if (_reentrantQueue.Count >= MaxReentrantDepth)
                    {
                        throw new InvalidOperationException(
                            $"Re-entrant queue depth exceeded maximum of {MaxReentrantDepth}. "
                            + $"Action '{action.GetType().Name}' aborted to prevent infinite loop.");
                    }

                    _logger.LogWarning(
                        "Re-entrant dispatch of {ActionType} while processing {CurrentActionType} — queued at depth {Depth}.",
                        action.GetType().Name,
                        _currentAction?.GetType().Name,
                        _reentrantQueue.Count + 1);

                    _eventPublisher.Publish(new ActionReentrantEventArgs(
                        action, _currentAction!, _reentrantQueue.Count + 1));
                    _reentrantQueue.Enqueue(action);
                    return;
                }

                // Different thread = concurrent dispatch — wait for current dispatch to finish
                while (_isDispatching)
                {
                    Monitor.Wait(_syncRoot);
                }

                // Re-check disposed after waiting
                if (_isDisposed)
                {
                    _logger.LogWarning(
                        "Action {ActionType} dispatched after store disposal — ignoring.",
                        action.GetType().Name);
                    return;
                }
            }

            _isDispatching = true;
            _dispatchingThreadId = Environment.CurrentManagedThreadId;
        }

        try
        {
            _currentAction = action;
            ProcessAction(action);
        }
        finally
        {
            // Drain re-entrant queue
            while (true)
            {
                object? nextAction;
                lock (_syncRoot)
                {
                    if (_reentrantQueue.Count == 0)
                    {
                        _isDispatching = false;
                        _dispatchingThreadId = 0;
                        _currentAction = null;
                        Monitor.PulseAll(_syncRoot);
                        break;
                    }

                    nextAction = _reentrantQueue.Dequeue();
                }

                try
                {
                    _currentAction = nextAction;
                    ProcessAction(nextAction);
                }
                catch (Exception ex)
                {
                    ActionContext context = new(nextAction) { StateProvider = _slices };
                    _eventPublisher.Publish(new ActionErrorEventArgs(ex, nextAction, context));
                    // Continue draining — don't let one failure block subsequent actions
                }
            }
        }
    }

    private void ProcessAction(object action)
    {
        ActionContext? context = null;

        try
        {
            // Create action context
            context = new ActionContext(action) { StateProvider = _slices };

            // First, check if any middleware wants to prevent this action
            if (!_pipeline.MayDispatchAction(action))
            {
                _eventPublisher.Publish(new ActionAbortedEventArgs(context, "Prevented by middleware"));
                return;
            }

            // Publish action started event
            _eventPublisher.Publish(new ActionStartedEventArgs(context));

            // Call BeforeReduce on all middlewares
            _pipeline.BeforeReduce(action);

            // Execute slice reducers
            foreach (ISlice slice in _slices.AllSlices)
            {
                slice.OnDispatch(action);
            }

            // Call AfterReduce on all middlewares
            _pipeline.AfterReduce(action);

            // Publish action completed event
            _eventPublisher.Publish(new ActionCompletedEventArgs(context));
        }
        catch (Exception ex)
        {
            // Create a context if we don't have one yet (error occurred during context creation)
            context ??= new ActionContext(action) { StateProvider = _slices };
            _eventPublisher.Publish(new ActionErrorEventArgs(ex, action, context));
            throw; // Re-throw to maintain existing behavior
        }
    }

    private void OnActionDispatched(object? sender, ActionDispatchedEventArgs e)
    {
        ProcessActionSafely(e.Action);
    }

    #region IStateProvider Implementation (delegated to _slices)

    /// <inheritdoc/>
    public TState GetSlice<TState>() => _slices.GetSlice<TState>();

    /// <inheritdoc/>
    public TState GetSliceByKey<TState>(string key) => _slices.GetSliceByKey<TState>(key);

    /// <inheritdoc/>
    public bool TryGetSlice<TState>(out TState? state) => _slices.TryGetSlice(out state);

    /// <inheritdoc/>
    public bool HasSlice<TState>() => _slices.HasSlice<TState>();

    /// <inheritdoc/>
    public bool HasSliceByKey(string key) => _slices.HasSliceByKey(key);

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetSliceKeys() => _slices.GetSliceKeys();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> GetAllSlices() => _slices.GetAllSlices();

    /// <inheritdoc/>
    public ImmutableSortedDictionary<string, object> GetStateDictionary() => _slices.GetStateDictionary();

    /// <inheritdoc/>
    public ImmutableSortedSet<string> GetKeys() => _slices.GetKeys();

    #endregion

    /// <inheritdoc/>
    public IReadOnlyList<string> GetSliceNames()
    {
        return _slices.AllSlices.Select(s => s.GetKey()).ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public IDisposable WhenSliceChanges<TState>(Action<TState> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        Type stateType = typeof(TState);
        TState? previousState = default;
        var hasPreviousState = false;

        void Handler(object? sender, StateChangedEventArgs e)
        {
            if (e.SliceType != stateType)
            {
                return;
            }

            var currentState = (TState)e.NewState;

            // Only notify if state actually changed
            if (hasPreviousState && EqualityComparer<TState>.Default.Equals(previousState, currentState))
            {
                return;
            }

            callback(currentState);
            previousState = currentState;
            hasPreviousState = true;
        }

        StateChanged += Handler;

        return new SliceChangeSubscription(() => StateChanged -= Handler);
    }

    /// <inheritdoc/>
    public IDisposable WhenSliceChanges<TState, TResult>(Func<TState, TResult> selector, Action<TResult> callback)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(callback);

        return WhenSliceChanges<TState>(state => callback(selector(state)));
    }

    private void OnSliceStateChanged(object? sender, StateChangedEventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }

    private sealed class SliceChangeSubscription : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public SliceChangeSubscription(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _dispose();
            _disposed = true;
        }
    }
}
