using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Ducky.Blazor;

/// <summary>
/// Renders <see cref="ChildContent"/> with one selected value and re-renders only itself when that value changes, so the
/// component around it does not re-render (SPEC §11.1, §11.2).
/// </summary>
/// <remarks>
/// A new <see cref="Selector"/> or <see cref="Comparer"/> reference re-subscribes. A lambda that captures variables is a
/// new reference on every render of the parent; that is correct, and costs one store subscription swap per render.
/// </remarks>
/// <typeparam name="TResult">The selected value's type.</typeparam>
public sealed class DuckySelect<TResult> : ComponentBase, IDisposable
{
    private SubscriptionCore? _core;
    private Selection<TResult>? _selection;

    /// <summary>Gets or sets the selector, which projects a snapshot to the value; it must be pure.</summary>
    [Parameter]
    [EditorRequired]
    public Func<StateSnapshot, TResult> Selector { get; set; } = null!;

    /// <summary>Gets or sets the content rendered with the selected value.</summary>
    [Parameter]
    public RenderFragment<TResult>? ChildContent { get; set; }

    /// <summary>Gets or sets what decides whether the value changed; <see cref="EqualityComparer{T}.Default"/> when null.</summary>
    [Parameter]
    public IEqualityComparer<TResult>? Comparer { get; set; }

    [Inject]
    private IStore Store { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><see cref="Selector"/> is null.</exception>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        var (selector, comparer) = (Selector, Comparer);
        parameters.SetParameterProperties(this);
        ArgumentNullException.ThrowIfNull(Selector);
        if (!ReferenceEquals(selector, Selector) || !ReferenceEquals(comparer, Comparer))
        {
            _core?.Dispose();
            _core = SubscriptionCore.Create(Store, Services, InvokeAsync, StateHasChanged);
            _selection = _core.Select(Selector, Comparer);
        }

        return base.SetParametersAsync(ParameterView.Empty);
    }

    /// <summary>Disposes the store subscription. Idempotent.</summary>
    public void Dispose() => _core?.Dispose();

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent, _selection!.Value);
}
