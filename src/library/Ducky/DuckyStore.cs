// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Ducky.Pipeline;

namespace Ducky;

/// <summary>
/// Represents a store that manages application state and handles actions.
/// </summary>
public sealed class DuckyStore : IStore, IDisposable
{
    private readonly IDispatcher _dispatcher;
    private readonly ActionPipeline _pipeline;
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly ObservableSlices _slices = new();
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly object _syncRoot = new();
    private volatile bool _isDisposed;
    private volatile bool _isDispatching;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckyStore"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher used to enqueue actions.</param>
    /// <param name="pipeline">The reactive action pipeline that processes actions.</param>
    /// <param name="eventPublisher">The event publisher for store events.</param>
    /// <param name="slices">The initial collection of slices to register.</param>
    public DuckyStore(
        IDispatcher dispatcher,
        ActionPipeline pipeline,
        IStoreEventPublisher eventPublisher,
        IEnumerable<ISlice> slices)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(slices);

        _dispatcher = dispatcher;
        _pipeline = pipeline;
        _eventPublisher = eventPublisher;

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

        // Initialize the pipeline
        _pipeline.InitializeAsync(_dispatcher, this).Wait();

        // Subscribe to action stream and process through pipeline
        _dispatcher.ActionDispatched += OnActionDispatched;

        // Dispatch initial action
        _dispatcher.Dispatch(new StoreInitialized());

        // Publish store initialized event
        _eventPublisher.Publish(new StoreInitializedEventArgs(sliceKeys.Count, sliceKeys));

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
    public int SliceCount => _slices.AllSlices.Count();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        TimeSpan uptime = DateTime.UtcNow - _startTime;
        _eventPublisher.Publish(new StoreDisposingEventArgs(uptime));

        _dispatcher.ActionDispatched -= OnActionDispatched;
        _slices.SliceStateChanged -= OnSliceStateChanged;
        _pipeline.Dispose();
        _slices.Dispose();

        _isDisposed = true;
    }

    private void ProcessActionSafely(object action)
    {
        // Prevent re-entrant processing
        if (_isDispatching)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_isDispatching)
            {
                return;
            }

            _isDispatching = true;

            try
            {
                ProcessAction(action);
            }
            finally
            {
                _isDispatching = false;
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
            ISlice? slice = _slices.AllSlices.FirstOrDefault(s => s.GetStateType() == stateType);
            if (slice is null)
            {
                return;
            }

            var currentState = (TState)slice.GetState();

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
