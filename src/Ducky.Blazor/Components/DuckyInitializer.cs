using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Ducky.Blazor;

/// <summary>
/// Renders <see cref="ChildContent"/> once the store is initialized (SPEC §11.1, §6.7). It awaits
/// <see cref="IStore.InitializeAsync"/> in <c>OnInitializedAsync</c>, so the static SSR and prerender quiescence wait
/// covers hydration and <see cref="ChildContent"/> renders hydrated state (§11.6). Optional and unstyled.
/// </summary>
public sealed class DuckyInitializer : ComponentBase
{
    private Task _init = null!;

    /// <summary>Gets or sets the content rendered once the store is initialized.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the content rendered until then (optional); it shows only under streaming rendering or interactively.
    /// </summary>
    [Parameter]
    public RenderFragment? Loading { get; set; }

    [Inject]
    private IStore Store { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <inheritdoc/>
    // The hand-off comes first (§11.2): init may need the renderer's services. The lifecycle task is the init task (never
    // faults): ComponentBase awaits it, then renders ChildContent.
    protected override Task OnInitializedAsync()
    {
        InteractivityGate.HandOver(Services, () => RendererInfo);
        return _init = Store.InitializeAsync();
    }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_init.IsCompleted)
        {
            builder.AddContent(0, ChildContent);
        }
        else
        {
            builder.AddContent(1, Loading);
        }
    }
}
