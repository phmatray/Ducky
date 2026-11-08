using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace StateContainerSample.Before;

#region before-component
// The component subscribes to OnChange, marshals to the renderer with InvokeAsync, and must unsubscribe on disposal.
public sealed class CounterView : ComponentBase, IDisposable
{
    [Inject]
    private CounterStore Store { get; set; } = null!;

    public void Increment() => Store.Increment(1);

    public void Dispose() => Store.OnChange -= OnStoreChanged;

    protected override async Task OnInitializedAsync()
    {
        Store.OnChange += OnStoreChanged;
        await Store.LoadAsync(CancellationToken.None);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, Store.IsLoading ? "loading" : Store.Count.ToString(CultureInfo.InvariantCulture));

    // OnChange runs on the thread that changed the store: after the await, that may not be the renderer's.
    private void OnStoreChanged() => _ = InvokeAsync(StateHasChanged);
}
#endregion
