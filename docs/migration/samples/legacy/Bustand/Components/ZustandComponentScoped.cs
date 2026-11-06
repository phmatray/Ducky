using Microsoft.AspNetCore.Components;
using Bustand.Core;

namespace Bustand.Components;

/// <summary>
/// Base component that receives store from CascadingParameter (via ZustandScope).
/// Use this when store is provided by parent ZustandScope rather than DI.
/// </summary>
/// <typeparam name="TStore">The store type.</typeparam>
/// <typeparam name="TState">The state type.</typeparam>
/// <remarks>
/// <para>
/// Unlike <see cref="ZustandComponent{TStore,TState}"/> which injects via DI,
/// this component receives the store from a parent <see cref="ZustandScope{TStore,TState}"/>.
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// &lt;ZustandScope TStore="CounterStore" TState="CounterState"&gt;
///     &lt;ScopedCounter /&gt;  &lt;!-- inherits ZustandComponentScoped --&gt;
/// &lt;/ZustandScope&gt;
/// </code>
/// </para>
/// </remarks>
public abstract class ZustandComponentScoped<TStore, TState> : ComponentBase, IDisposable
    where TStore : ZustandStore<TState>
    where TState : class
{
    private readonly List<ISubscription> _subscriptions = new();
    private bool _disposed;

    /// <summary>
    /// The store instance, received from parent ZustandScope via CascadingParameter.
    /// </summary>
    [CascadingParameter]
    protected TStore Store { get; set; } = default!;

    /// <inheritdoc cref="ZustandComponent{TStore,TState}.UseState{TSlice}"/>
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

    /// <inheritdoc cref="ZustandComponent{TStore,TState}.UseState"/>
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

    protected override void OnAfterRender(bool firstRender)
    {
        Store.EndRender();
        base.OnAfterRender(firstRender);
    }

    protected override bool ShouldRender()
    {
        Store.BeginRender();
        return base.ShouldRender();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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
