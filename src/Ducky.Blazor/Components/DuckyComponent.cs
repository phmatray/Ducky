using Microsoft.AspNetCore.Components;

namespace Ducky.Blazor;

/// <summary>
/// The base class of a component that reads the store (SPEC §11.1, §11.2). Register selections with
/// <see cref="Select{T}(Func{StateSnapshot, T}, IEqualityComparer{T})"/> in <c>OnInitialized</c>; the component re-renders
/// only when a selected value changes, compared on the renderer with the value it last rendered.
/// </summary>
public abstract class DuckyComponent : ComponentBase, IHandleAfterRender, IDisposable
{
    private readonly ComponentSelections _selections = new();

    /// <summary>Gets the store.</summary>
    [Inject]
    protected IStore Store { get; private set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

#pragma warning disable RS0026 // justification: SPEC §11.1 fixes both Select overloads, each with an optional comparer
    /// <summary>
    /// Registers a selection. Its <see cref="Selection{T}.Value"/> evaluates on read, so a selector may read parameters and
    /// fields. Call it before the first render (in <c>OnInitialized</c>).
    /// </summary>
    /// <remarks>
    /// Read <see cref="Selection{T}.Value"/> only on the component's renderer (render, lifecycle methods, event handlers),
    /// like any component state. Each read is the value the change check compares against, so a read from another
    /// thread, such as a timer callback, can hide a change until the next commit. Marshal such work with
    /// <c>InvokeAsync</c>.
    /// </remarks>
    /// <typeparam name="T">The selected value's type.</typeparam>
    /// <param name="selector">Projects a snapshot to the value; must be pure.</param>
    /// <param name="comparer">Decides whether the value changed; <see cref="EqualityComparer{T}.Default"/> when null.</param>
    /// <returns>The selection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
    /// <exception cref="InvalidOperationException">DUCKY352: called after the component's first render.</exception>
    protected Selection<T> Select<T>(Func<StateSnapshot, T> selector, IEqualityComparer<T>? comparer = null) =>
        _selections.Select(this, Store, Services, () => RendererInfo, InvokeAsync, StateHasChanged, selector, comparer);

    /// <summary>Registers a selection over one slice's state; see <see cref="Select{T}(Func{StateSnapshot, T}, IEqualityComparer{T})"/>.</summary>
    /// <typeparam name="TState">The slice's state type.</typeparam>
    /// <typeparam name="T">The selected value's type.</typeparam>
    /// <param name="selector">Projects the slice's state to the value; must be pure.</param>
    /// <param name="comparer">Decides whether the value changed; <see cref="EqualityComparer{T}.Default"/> when null.</param>
    /// <returns>The selection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
    /// <exception cref="InvalidOperationException">DUCKY352: called after the component's first render.</exception>
    protected Selection<T> Select<TState, T>(Func<TState, T> selector, IEqualityComparer<T>? comparer = null)
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Select(state => selector(state.Get<TState>()), comparer);
    }

#pragma warning restore RS0026

    /// <summary>Dispatches an action to <see cref="Store"/>.</summary>
    /// <param name="action">The action.</param>
    protected void Dispatch(object action) => Store.Dispatch(action);

    /// <summary>Dispatches an action to <see cref="Store"/> and completes when it was processed; it never faults.</summary>
    /// <param name="action">The action.</param>
    /// <returns>How the action ended.</returns>
    protected Task<DispatchResult> DispatchAsync(object action) => Store.DispatchAsync(action);

    /// <summary>Disposes the component's store subscription. Idempotent.</summary>
    /// <remarks>
    /// Blazor calls only <see cref="IAsyncDisposable.DisposeAsync"/> on a component that implements
    /// <see cref="IAsyncDisposable"/>, so a subclass that adds it must call <see cref="Dispose()"/> from its
    /// <c>DisposeAsync</c>; otherwise the component stays subscribed to the store and its selectors keep running.
    /// </remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        // Stryker disable once Statement : equivalent, the class has no finalizer; CA1816 asks for the call for subclasses that add one
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the component's store subscription when <paramref name="disposing"/> is true.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _selections.Dispose();
        }
    }

    // Mirrors ComponentBase, and closes registration before any user override runs, so no override can skip it (INV-20).
    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        BeforeRegistrationCloses();
        var first = _selections.CloseRegistration();
        OnAfterRender(first);
        return OnAfterRenderAsync(first);
    }

    // DuckyComponent<TState> creates its whole-slice selection here at the latest.
    private protected virtual void BeforeRegistrationCloses()
    {
    }
}
