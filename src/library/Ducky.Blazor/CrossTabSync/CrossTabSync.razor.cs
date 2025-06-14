namespace Ducky.Blazor.CrossTabSync;

/// <summary>
/// A Blazor component that synchronizes state across browser tabs.
/// </summary>
public partial class CrossTabSync
{
    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await TabSync.StartAsync().ConfigureAwait(false);
    }
}
