using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace R3dux.Blazor;

public abstract class R3duxLayout<TState>
    : R3duxComponent<TState>
    where TState : notnull, new()
{
    /// <summary>
    /// Gets the content to be rendered inside the layout.
    /// </summary>
    [Parameter]
    public RenderFragment? Body { get; set; }

    /// <inheritdoc />
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof (LayoutComponentBase))]
    public override Task SetParametersAsync(ParameterView parameters)
    {
        return base.SetParametersAsync(parameters);
    }
}