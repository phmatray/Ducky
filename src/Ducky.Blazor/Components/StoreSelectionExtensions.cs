using Microsoft.AspNetCore.Components;

namespace Ducky.Blazor;

/// <summary>Selections for a component that can't derive from <see cref="DuckyComponent"/> (SPEC §11.1, CMP-05).</summary>
public static class StoreSelectionExtensions
{
    /// <summary>
    /// Selects a value for a component with another base class: the component re-renders when the value changes, compared
    /// on the renderer with the value it last read, like <see cref="DuckyComponent"/>'s <c>Select</c>.
    /// </summary>
    /// <remarks>
    /// Call it before the first render (in <c>OnInitialized</c>), read <see cref="Selection{T}.Value"/> only on the
    /// component's renderer, and dispose the selection in the component's <c>Dispose</c>, which stops its store
    /// subscription. Each call takes its own store subscription, because this API has no handle on the component: the
    /// INV-19 bound of at most one pending <c>InvokeAsync</c> holds per selection here, not per component. When several
    /// change in one commit, the first re-render reads them all, so the others find nothing new and the component still
    /// renders once per commit.
    /// </remarks>
    /// <typeparam name="T">The selected value's type.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="selector">Projects a snapshot to the value; must be pure.</param>
    /// <param name="invokeAsync">The component's <c>InvokeAsync</c>.</param>
    /// <param name="stateHasChanged">The component's <c>StateHasChanged</c>.</param>
    /// <param name="rendererInfo">The component's <c>RendererInfo</c>: the first toucher tells the store whether it renders interactively.</param>
    /// <param name="services">The component's injected <see cref="IServiceProvider"/>, handed over to the store as its renderer scope.</param>
    /// <param name="comparer">Decides whether the value changed; <see cref="EqualityComparer{T}.Default"/> when null.</param>
    /// <returns>The selection.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="comparer"/> is null.</exception>
    public static Selection<T> Select<T>(
        this IStore store,
        Func<StateSnapshot, T> selector,
        Func<Func<Task>, Task> invokeAsync,
        Action stateHasChanged,
        RendererInfo rendererInfo,
        IServiceProvider services,
        IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(invokeAsync);
        ArgumentNullException.ThrowIfNull(stateHasChanged);
        ArgumentNullException.ThrowIfNull(rendererInfo);
        ArgumentNullException.ThrowIfNull(services);
        var core = SubscriptionCore.Create(store, services, () => rendererInfo, invokeAsync, stateHasChanged);
        var selection = core.Select(selector, comparer);
        return Selection<T>.Create(() => selection.Value, onDispose: core.Dispose);
    }
}
