using Microsoft.AspNetCore.Components;
using Bustand.Core;

namespace Bustand.Components;

/// <summary>
/// Base component class for consuming Bustand stores with automatic subscription management.
/// </summary>
/// <typeparam name="TStore">The store type.</typeparam>
/// <typeparam name="TState">The state type.</typeparam>
/// <remarks>
/// <para>
/// Inherit from this class to get automatic subscription management.
/// Use <see cref="UseState{TSlice}"/> to subscribe to state slices.
/// Subscriptions are automatically disposed when the component is disposed.
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// public class Counter : ZustandComponent&lt;CounterStore, CounterState&gt;
/// {
///     private UseStateResult&lt;int&gt; _count;
///
///     protected override void OnInitialized()
///     {
///         _count = UseState(s => s.Count);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class ZustandComponent<TStore, TState> : ComponentBase, IDisposable
    where TStore : ZustandStore<TState>
    where TState : class
{
    private readonly List<ISubscription> _subscriptions = new();
    private bool _disposed;

    /// <summary>
    /// The store instance, injected via DI.
    /// </summary>
    [Inject]
    protected TStore Store { get; set; } = default!;

    /// <summary>
    /// Subscribe to a state slice. Returns current value and re-renders when slice changes.
    /// </summary>
    /// <typeparam name="TSlice">The type of the state slice.</typeparam>
    /// <param name="selector">Function to select the state slice.</param>
    /// <returns>UseStateResult providing access to current value.</returns>
    /// <example>
    /// <code>
    /// var count = UseState(s => s.Count);
    /// // In markup: @count.Value or just @count (implicit conversion)
    /// </code>
    /// </example>
    protected UseStateResult<TSlice> UseState<TSlice>(Func<TState, TSlice> selector)
    {
        var subscription = Store.Subscribe(selector, () =>
        {
            if (!_disposed)
            {
                try
                {
                    InvokeAsync(StateHasChanged);
                }
                catch (ObjectDisposedException)
                {
                    // Component disposed during callback - ignore
                }
            }
        });
        _subscriptions.Add(subscription);
        return new UseStateResult<TSlice>(() => selector(Store.State));
    }

    /// <summary>
    /// Subscribe to all state changes.
    /// </summary>
    /// <returns>UseStateResult providing access to full state.</returns>
    protected UseStateResult<TState> UseState()
    {
        var subscription = Store.Subscribe(() =>
        {
            if (!_disposed)
            {
                try
                {
                    InvokeAsync(StateHasChanged);
                }
                catch (ObjectDisposedException)
                {
                    // Component disposed during callback - ignore
                }
            }
        });
        _subscriptions.Add(subscription);
        return new UseStateResult<TState>(() => Store.State);
    }

    /// <summary>
    /// Called after rendering. Notifies store that render phase ended.
    /// </summary>
    protected override void OnAfterRender(bool firstRender)
    {
        Store.EndRender();
        base.OnAfterRender(firstRender);
    }

    /// <summary>
    /// Called before render. Sets render flag for loop detection.
    /// </summary>
    protected override bool ShouldRender()
    {
        Store.BeginRender();
        return base.ShouldRender();
    }

    /// <summary>
    /// Disposes all subscriptions.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to add custom disposal logic. Always call base.Dispose(disposing).
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var sub in _subscriptions)
                {
                    sub.Dispose();
                }
                _subscriptions.Clear();
            }
            _disposed = true;
        }
    }
}
