using System.Runtime.CompilerServices;
using Bustand.Middleware;

namespace Bustand.Core;

/// <summary>
/// Abstract base class for Bustand stores. Inherit from this class to create a store.
/// </summary>
/// <typeparam name="TState">The state type. Use a C# record for immutability with 'with' expressions.</typeparam>
/// <example>
/// <code>
/// public record CounterState(int Count = 0);
///
/// [BustandStore]
/// public class CounterStore : ZustandStore&lt;CounterState&gt;
/// {
///     protected override CounterState InitialState => new CounterState();
///
///     public void Increment() => Set(s => s with { Count = s.Count + 1 });
/// }
/// </code>
/// </example>
/// <remarks>
/// <para>
/// <b>Thread Safety (MODE-05):</b> State updates are thread-safe via locking.
/// When subscribing to StateChanged in Blazor Server components, always use
/// <c>InvokeAsync(StateHasChanged)</c> in your event handler for proper
/// synchronization context handling.
/// </para>
/// <para>
/// <b>SetAsync for background threads:</b> Use <see cref="SetAsync(Func{TState, TState})"/>
/// when updating state from background threads. It automatically handles
/// SynchronizationContext marshalling.
/// </para>
/// <para>
/// <b>Render loop protection:</b> Calling Set() during a component's render phase
/// will throw <see cref="RenderLoopException"/> to prevent infinite loops.
/// </para>
/// </remarks>
public abstract class ZustandStore<TState> : IStore<TState> where TState : class
{
    private TState? _state;
    private readonly object _lock = new();
    private readonly List<ISubscription> _subscriptions = new();
    private readonly object _subscriptionLock = new();
    private bool _isRendering;
    private bool _isInitialized;
    private bool _isInitializing;
    private bool _stateInitialized;
    private MiddlewarePipeline<TState> _pipeline = MiddlewarePipeline<TState>.Empty;

    /// <summary>
    /// Gets the initial state for this store.
    /// </summary>
    /// <remarks>
    /// Override this property to provide the initial state for your store.
    /// This is called once when the store is first accessed.
    /// </remarks>
    protected abstract TState InitialState { get; }

    /// <summary>
    /// Sets the middleware pipeline. Called by DI during store construction.
    /// </summary>
    /// <param name="pipeline">The pipeline to set, or null for empty pipeline.</param>
    internal void SetPipeline(MiddlewarePipeline<TState> pipeline)
    {
        _pipeline = pipeline ?? MiddlewarePipeline<TState>.Empty;
    }

    /// <summary>
    /// Sets the initial state from persistence restoration.
    /// Called by DI factory before the store is returned to consumer.
    /// </summary>
    /// <param name="restoredState">The state restored from storage, or null to use InitialState.</param>
    /// <remarks>
    /// This method is called only once during store construction, before any Set() calls.
    /// If restoredState is null or invalid, InitialState is used instead.
    /// </remarks>
    internal void SetRestoredState(TState? restoredState)
    {
        if (restoredState is null)
            return;

        // Only restore if state hasn't been initialized yet
        lock (_lock)
        {
            if (_stateInitialized)
                return;

            _state = restoredState;
            _stateInitialized = true;
        }
    }

    /// <summary>
    /// Gets the initial state. Used by persistence to merge with restored state.
    /// </summary>
    internal TState GetInitialState() => InitialState;

    /// <inheritdoc />
    public TState State
    {
        get
        {
            EnsureStateInitialized();
            return _state!;
        }
    }

    /// <inheritdoc />
    public bool IsInitialized => _isInitialized;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <summary>
    /// Updates the state using the provided mutator function.
    /// The mutator receives the current state and should return a new state (use 'with' expression for records).
    /// </summary>
    /// <param name="mutator">A function that takes current state and returns new state.</param>
    /// <param name="actionName">Optional action name for middleware context. Defaults to caller method name.</param>
    /// <exception cref="RenderLoopException">
    /// Thrown when Set() is called during a component's render phase.
    /// </exception>
    /// <example>
    /// <code>
    /// // For record state:
    /// Set(state => state with { Count = state.Count + 1 });
    /// </code>
    /// </example>
    protected void Set(Func<TState, TState> mutator, [CallerMemberName] string? actionName = null)
    {
        ThrowIfRendering();
        EnsureStateInitialized();

        TState oldState;
        TState newState;

        lock (_lock)
        {
            oldState = _state!;
            newState = mutator(oldState);
        }

        // Create middleware context
        var context = new MiddlewareContext<TState>
        {
            OldState = oldState,
            NewState = newState,
            StoreType = GetType(),
            ActionName = actionName,
            Timestamp = DateTimeOffset.UtcNow
        };

        // BeforeChange - can block
        if (!_pipeline.InvokeBeforeChange(context))
            return; // State change blocked by middleware

        lock (_lock)
        {
            _state = newState;
        }

        // AfterChange - cannot block
        _pipeline.InvokeAfterChange(context);

        OnStateChanged();
    }

    /// <summary>
    /// Updates the state to the specified new state, replacing the current state entirely.
    /// </summary>
    /// <param name="newState">The new state to set.</param>
    /// <param name="actionName">Optional action name for middleware context. Defaults to caller method name.</param>
    /// <exception cref="ArgumentNullException">Thrown when newState is null.</exception>
    /// <exception cref="RenderLoopException">
    /// Thrown when Set() is called during a component's render phase.
    /// </exception>
    /// <example>
    /// <code>
    /// // Direct state replacement:
    /// Set(new CounterState(42));
    /// </code>
    /// </example>
    protected void Set(TState newState, [CallerMemberName] string? actionName = null)
    {
        ArgumentNullException.ThrowIfNull(newState);
        ThrowIfRendering();
        EnsureStateInitialized();

        TState oldState;

        lock (_lock)
        {
            oldState = _state!;
        }

        var context = new MiddlewareContext<TState>
        {
            OldState = oldState,
            NewState = newState,
            StoreType = GetType(),
            ActionName = actionName,
            Timestamp = DateTimeOffset.UtcNow
        };

        if (!_pipeline.InvokeBeforeChange(context))
            return;

        lock (_lock)
        {
            _state = newState;
        }

        _pipeline.InvokeAfterChange(context);
        OnStateChanged();
    }

    /// <summary>
    /// Asynchronously updates the state using the provided mutator function.
    /// Handles SynchronizationContext marshalling for background thread safety.
    /// </summary>
    /// <param name="mutator">A function that takes current state and returns new state.</param>
    /// <param name="actionName">Optional action name for middleware context. Defaults to caller method name.</param>
    /// <returns>A task that completes when the state change notification has been processed.</returns>
    /// <remarks>
    /// Use this method when updating state from background threads (e.g., from async APIs,
    /// timers, or Task.Run). It ensures the StateChanged event is raised on the correct
    /// synchronization context if one is available.
    /// </remarks>
    /// <example>
    /// <code>
    /// public async Task LoadDataAsync()
    /// {
    ///     var data = await _api.GetDataAsync();
    ///     await SetAsync(state => state with { Data = data });
    /// }
    /// </code>
    /// </example>
    protected async Task SetAsync(Func<TState, TState> mutator, [CallerMemberName] string? actionName = null)
    {
        EnsureStateInitialized();
        var syncContext = SynchronizationContext.Current;

        TState oldState;
        TState newState;

        lock (_lock)
        {
            oldState = _state!;
            newState = mutator(oldState);
        }

        var middlewareContext = new MiddlewareContext<TState>
        {
            OldState = oldState,
            NewState = newState,
            StoreType = GetType(),
            ActionName = actionName,
            Timestamp = DateTimeOffset.UtcNow
        };

        if (!_pipeline.InvokeBeforeChange(middlewareContext))
            return; // State change blocked by middleware

        lock (_lock)
        {
            _state = newState;
        }

        _pipeline.InvokeAfterChange(middlewareContext);

        if (syncContext != null)
        {
            var tcs = new TaskCompletionSource();
            syncContext.Post(_ =>
            {
                OnStateChanged();
                tcs.SetResult();
            }, null);
            await tcs.Task;
        }
        else
        {
            OnStateChanged();
        }
    }

    /// <summary>
    /// Asynchronously updates the state to the specified new state.
    /// Handles SynchronizationContext marshalling for background thread safety.
    /// </summary>
    /// <param name="newState">The new state to set.</param>
    /// <param name="actionName">Optional action name for middleware context. Defaults to caller method name.</param>
    /// <returns>A task that completes when the state change notification has been processed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when newState is null.</exception>
    /// <remarks>
    /// Use this method when updating state from background threads.
    /// </remarks>
    protected async Task SetAsync(TState newState, [CallerMemberName] string? actionName = null)
    {
        ArgumentNullException.ThrowIfNull(newState);
        EnsureStateInitialized();
        var syncContext = SynchronizationContext.Current;

        TState oldState;

        lock (_lock)
        {
            oldState = _state!;
        }

        var middlewareContext = new MiddlewareContext<TState>
        {
            OldState = oldState,
            NewState = newState,
            StoreType = GetType(),
            ActionName = actionName,
            Timestamp = DateTimeOffset.UtcNow
        };

        if (!_pipeline.InvokeBeforeChange(middlewareContext))
            return; // State change blocked by middleware

        lock (_lock)
        {
            _state = newState;
        }

        _pipeline.InvokeAfterChange(middlewareContext);

        if (syncContext != null)
        {
            var tcs = new TaskCompletionSource();
            syncContext.Post(_ =>
            {
                OnStateChanged();
                tcs.SetResult();
            }, null);
            await tcs.Task;
        }
        else
        {
            OnStateChanged();
        }
    }

    /// <summary>
    /// Subscribe to all state changes. Callback invoked on every Set() call.
    /// </summary>
    /// <param name="callback">Action to invoke when state changes.</param>
    /// <returns>Subscription handle. Dispose to unsubscribe.</returns>
    /// <example>
    /// <code>
    /// var sub = store.Subscribe(() => Console.WriteLine("State changed!"));
    /// // Later: sub.Dispose();
    /// </code>
    /// </example>
    public ISubscription Subscribe(Action callback)
    {
        var subscription = new FullStateSubscription<TState>(callback, RemoveSubscription);
        lock (_subscriptionLock)
        {
            _subscriptions.Add(subscription);
        }
        return subscription;
    }

    /// <summary>
    /// Subscribe to changes in a specific state slice. Callback only invoked when selected value changes.
    /// </summary>
    /// <typeparam name="TSlice">The type of the selected state slice.</typeparam>
    /// <param name="selector">Function to select the state slice to watch.</param>
    /// <param name="callback">Action to invoke when selected slice changes.</param>
    /// <returns>Subscription handle. Dispose to unsubscribe.</returns>
    /// <remarks>
    /// Uses reference equality to determine if the slice has changed, which works naturally
    /// with C# records. The callback is only invoked when the selected slice reference changes.
    /// </remarks>
    /// <example>
    /// <code>
    /// var sub = store.Subscribe(s => s.Count, () => Console.WriteLine("Count changed!"));
    /// // Later: sub.Dispose();
    /// </code>
    /// </example>
    public ISubscription Subscribe<TSlice>(Func<TState, TSlice> selector, Action callback)
    {
        EnsureStateInitialized();
        var subscription = new Subscription<TState, TSlice>(
            selector,
            callback,
            RemoveSubscription,
            State);
        lock (_subscriptionLock)
        {
            _subscriptions.Add(subscription);
        }
        return subscription;
    }

    /// <summary>
    /// Gets the current number of active subscriptions. For testing/debugging.
    /// </summary>
    internal int SubscriptionCount
    {
        get { lock (_subscriptionLock) return _subscriptions.Count; }
    }

    private void RemoveSubscription(ISubscription subscription)
    {
        lock (_subscriptionLock)
        {
            _subscriptions.Remove(subscription);
        }
    }

    /// <summary>
    /// Override this method to perform async initialization when the store is first resolved.
    /// </summary>
    /// <returns>A task representing the initialization operation.</returns>
    /// <remarks>
    /// <para>
    /// This method is called automatically when the store is first resolved from DI.
    /// Use it to load initial data from APIs, databases, or other async sources.
    /// </para>
    /// <para>
    /// The store's <see cref="InitialState"/> is available immediately, even before
    /// this method completes. This allows components to render with initial state
    /// while async initialization happens in the background.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// protected override async Task InitializeAsync()
    /// {
    ///     var data = await _api.GetInitialDataAsync();
    ///     Set(state => state with { Data = data, IsLoaded = true });
    /// }
    /// </code>
    /// </example>
    protected virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Ensures the store is initialized. Called by DI activation.
    /// </summary>
    /// <returns>A task that completes when initialization is done.</returns>
    internal async Task EnsureInitializedAsync()
    {
        if (_isInitialized || _isInitializing)
        {
            return;
        }

        _isInitializing = true;
        try
        {
            await InitializeAsync();
            _isInitialized = true;
        }
        finally
        {
            _isInitializing = false;
        }
    }

    /// <summary>
    /// Marks the beginning of a component render phase.
    /// Called by component base classes before StateHasChanged.
    /// </summary>
    internal void BeginRender()
    {
        _isRendering = true;
    }

    /// <summary>
    /// Marks the end of a component render phase.
    /// Called by component base classes after StateHasChanged.
    /// </summary>
    internal void EndRender()
    {
        _isRendering = false;
    }

    /// <summary>
    /// Raises the StateChanged event and notifies all subscriptions of state changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is called automatically after each <see cref="Set(Func{TState, TState})"/> call.
    /// Override to add custom behavior such as logging or validation.
    /// </para>
    /// <para>
    /// <b>Important for Blazor Server (MODE-05):</b> Legacy event subscribers must use
    /// <c>InvokeAsync(StateHasChanged)</c> in their event handlers to ensure
    /// proper synchronization context handling. Selector-based subscriptions handle this
    /// more elegantly through the subscription system.
    /// </para>
    /// </remarks>
    protected virtual void OnStateChanged()
    {
        // Notify legacy event subscribers
        StateChanged?.Invoke(this, EventArgs.Empty);

        // Notify subscription-based subscribers
        NotifySubscriptions();
    }

    private void NotifySubscriptions()
    {
        List<ISubscription> subs;
        lock (_subscriptionLock)
        {
            subs = _subscriptions.ToList(); // Copy for thread safety
        }

        var state = State;
        foreach (var sub in subs)
        {
            try
            {
                if (sub is IInternalSubscription<TState> internalSub)
                {
                    internalSub.NotifyStateChanged(state);
                }
            }
            catch (ObjectDisposedException)
            {
                // Component disposed during notification - silently ignore per CONTEXT.md
                // "Silently ignore subscriptions during component disposal (graceful degradation)"
            }
        }
    }

    private void EnsureStateInitialized()
    {
        if (_stateInitialized)
        {
            return;
        }

        lock (_lock)
        {
            if (_stateInitialized)
            {
                return;
            }

            _state = InitialState ?? throw new InvalidOperationException(
                $"InitialState property of {GetType().Name} cannot return null.");
            _stateInitialized = true;
        }
    }

    private void ThrowIfRendering()
    {
        if (_isRendering)
        {
            throw new RenderLoopException(GetType());
        }
    }
}
