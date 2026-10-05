using Microsoft.AspNetCore.Components;

namespace Ducky.Blazor;

/// <summary>A layout that reads the store, with the same members as <see cref="DuckyComponent"/> (SPEC §11.1, §11.2).</summary>
public abstract class DuckyLayout : LayoutComponentBase, IHandleAfterRender, IDisposable
{
    private readonly ComponentSelections _selections = new();

    /// <summary>Gets the store.</summary>
    [Inject]
    protected IStore Store { get; private set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

#pragma warning disable RS0026 // justification: SPEC §11.1 fixes both Select overloads, each with an optional comparer
    /// <inheritdoc cref="DuckyComponent.Select{T}(Func{StateSnapshot, T}, IEqualityComparer{T})"/>
    protected Selection<T> Select<T>(Func<StateSnapshot, T> selector, IEqualityComparer<T>? comparer = null) =>
        _selections.Select(this, Store, Services, () => RendererInfo, InvokeAsync, StateHasChanged, selector, comparer);

    /// <inheritdoc cref="DuckyComponent.Select{TState, T}(Func{TState, T}, IEqualityComparer{T})"/>
    protected Selection<T> Select<TState, T>(Func<TState, T> selector, IEqualityComparer<T>? comparer = null)
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Select(state => selector(state.Get<TState>()), comparer);
    }

#pragma warning restore RS0026

    /// <inheritdoc cref="DuckyComponent.Dispatch(object)"/>
    protected void Dispatch(object action) => Store.Dispatch(action);

    /// <inheritdoc cref="DuckyComponent.DispatchAsync(object)"/>
    protected Task<DispatchResult> DispatchAsync(object action) => Store.DispatchAsync(action);

    /// <inheritdoc cref="DuckyComponent.Dispose()"/>
    public void Dispose()
    {
        Dispose(disposing: true);
        // Stryker disable once Statement : equivalent, the class has no finalizer; CA1816 asks for the call for subclasses that add one
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc cref="DuckyComponent.Dispose(bool)"/>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _selections.Dispose();
        }
    }

    // Mirrors ComponentBase, and closes registration before any user override runs (INV-20).
    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        var first = _selections.CloseRegistration();
        OnAfterRender(first);
        return OnAfterRenderAsync(first);
    }
}
