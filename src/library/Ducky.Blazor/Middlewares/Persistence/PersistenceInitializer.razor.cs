using Microsoft.AspNetCore.Components;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// A Blazor component that initializes state persistence functionality.
/// </summary>
public partial class PersistenceInitializer : ComponentBase
{
    [Inject]
    private IPersistenceService PersistenceService { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically hydrate on first render.
    /// Default is true.
    /// </summary>
    [Parameter]
    public bool AutoHydrate { get; set; } = true;

    /// <summary>
    /// Gets or sets the content to render while hydration is in progress.
    /// </summary>
    [Parameter]
    public RenderFragment? LoadingContent { get; set; }

    /// <summary>
    /// Gets or sets the content to render after hydration completes.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the callback to invoke when hydration completes.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnHydrationCompleted { get; set; }

    /// <summary>
    /// Gets a value indicating whether hydration is in progress.
    /// </summary>
    protected bool IsHydrating { get; private set; }

    /// <summary>
    /// Gets a value indicating whether hydration has completed.
    /// </summary>
    protected bool IsHydrated => PersistenceService.IsHydrated;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !AutoHydrate || IsHydrated)
        {
            return;
        }

        await HydrateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Manually triggers hydration.
    /// </summary>
    public async Task HydrateAsync()
    {
        if (IsHydrating || IsHydrated)
        {
            return;
        }

        IsHydrating = true;
        StateHasChanged();

        try
        {
            await PersistenceService
                .HydrateAsync()
                .ConfigureAwait(false);

            if (OnHydrationCompleted.HasDelegate)
            {
                await OnHydrationCompleted
                    .InvokeAsync(true)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            if (OnHydrationCompleted.HasDelegate)
            {
                await OnHydrationCompleted
                    .InvokeAsync(false)
                    .ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            IsHydrating = false;
            StateHasChanged();
        }
    }
}
