namespace Bustand.Core;

/// <summary>
/// Internal interface for subscription notification.
/// Allows the store to notify subscriptions without knowing the generic type parameters.
/// </summary>
/// <typeparam name="TState">The state type of the store.</typeparam>
internal interface IInternalSubscription<TState> where TState : class
{
    /// <summary>
    /// Notifies the subscription that state has changed.
    /// </summary>
    /// <param name="newState">The new state.</param>
    void NotifyStateChanged(TState newState);
}

/// <summary>
/// Internal subscription implementation with selector-based change detection.
/// </summary>
/// <typeparam name="TState">The state type of the store.</typeparam>
/// <typeparam name="TSlice">The type of the selected state slice.</typeparam>
/// <remarks>
/// Uses reference equality to determine if the selected slice has changed,
/// which works naturally with C# records (per CONTEXT.md decision).
/// </remarks>
internal sealed class Subscription<TState, TSlice> : ISubscription, IInternalSubscription<TState>
    where TState : class
{
    private readonly Func<TState, TSlice> _selector;
    private readonly Action _callback;
    private readonly Action<ISubscription> _onDispose;
    private TSlice? _lastValue;
    private bool _isActive = true;

    /// <inheritdoc />
    public bool IsActive => _isActive;

    /// <summary>
    /// Creates a new selector-based subscription.
    /// </summary>
    /// <param name="selector">Function to select the state slice to watch.</param>
    /// <param name="callback">Action to invoke when the selected slice changes.</param>
    /// <param name="onDispose">Callback to remove subscription from store's tracking list.</param>
    /// <param name="initialState">The current state to capture initial slice value.</param>
    public Subscription(
        Func<TState, TSlice> selector,
        Action callback,
        Action<ISubscription> onDispose,
        TState initialState)
    {
        _selector = selector;
        _callback = callback;
        _onDispose = onDispose;
        _lastValue = selector(initialState);
    }

    /// <summary>
    /// Called when store state changes. Only invokes callback if selected slice changed.
    /// </summary>
    /// <param name="newState">The new state.</param>
    public void NotifyStateChanged(TState newState)
    {
        if (!_isActive) return;

        var newValue = _selector(newState);

        // Use reference equality for records (per CONTEXT.md decision)
        if (!ReferenceEquals(_lastValue, newValue))
        {
            _lastValue = newValue;
            _callback();
        }
    }

    /// <inheritdoc />
    public void Unsubscribe() => Dispose();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isActive)
        {
            _isActive = false;
            _onDispose(this);
        }
    }
}

/// <summary>
/// Subscription for full state (no selector, always notifies on any state change).
/// </summary>
/// <typeparam name="TState">The state type of the store.</typeparam>
internal sealed class FullStateSubscription<TState> : ISubscription, IInternalSubscription<TState>
    where TState : class
{
    private readonly Action _callback;
    private readonly Action<ISubscription> _onDispose;
    private bool _isActive = true;

    /// <inheritdoc />
    public bool IsActive => _isActive;

    /// <summary>
    /// Creates a new full-state subscription.
    /// </summary>
    /// <param name="callback">Action to invoke on every state change.</param>
    /// <param name="onDispose">Callback to remove subscription from store's tracking list.</param>
    public FullStateSubscription(Action callback, Action<ISubscription> onDispose)
    {
        _callback = callback;
        _onDispose = onDispose;
    }

    /// <summary>
    /// Called when store state changes. Always invokes callback for full-state subscriptions.
    /// </summary>
    /// <param name="newState">The new state (unused, but required by interface).</param>
    public void NotifyStateChanged(TState newState)
    {
        if (_isActive) _callback();
    }

    /// <inheritdoc />
    public void Unsubscribe() => Dispose();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isActive)
        {
            _isActive = false;
            _onDispose(this);
        }
    }
}
