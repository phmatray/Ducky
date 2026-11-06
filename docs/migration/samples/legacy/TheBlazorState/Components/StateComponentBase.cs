using Microsoft.AspNetCore.Components;
using TheBlazorState.Storage;

namespace TheBlazorState.Components;

/// <summary>
/// Base class for components that need automatic re-rendering when
/// cross-tab sync delivers state updates from other browser tabs.
///
/// Inherit from this instead of <see cref="ComponentBase"/> in components
/// that use <c>[Persist]</c> properties with <c>LocalStorage</c>.
///
/// Conceptually similar to Fluxor's FluxorComponent — subscribes to
/// state change notifications and ensures <see cref="ComponentBase.StateHasChanged"/>
/// is called on the correct synchronization context.
/// </summary>
public abstract class StateComponentBase : ComponentBase, IDisposable
{
    [Inject]
    private CrossTabSyncService CrossTabSync { get; set; } = null!;

    private bool _disposed;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        CrossTabSync.AfterCrossTabChange += OnCrossTabChange;
    }

    private void OnCrossTabChange()
    {
        if (!_disposed)
        {
            InvokeAsync(StateHasChanged);
        }
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CrossTabSync.AfterCrossTabChange -= OnCrossTabChange;
    }
}
