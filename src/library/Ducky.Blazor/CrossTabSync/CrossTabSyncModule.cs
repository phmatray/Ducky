using Microsoft.JSInterop;

namespace Ducky.Blazor.CrossTabSync;

/// <summary>
/// Enables automatic Redux state synchronization across browser tabs using localStorage events.
/// </summary>
public class CrossTabSyncModule : JsModule
{
    private readonly DotNetObjectReference<CrossTabSyncModule> _objRef;
    private readonly IStore _store;
    private readonly string _key;

    /// <summary>
    /// Constructs a new instance for cross-tab sync.
    /// </summary>
    /// <param name="js">Blazor JS runtime.</param>
    /// <param name="store">The Redux store instance to synchronize.</param>
    /// <param name="key">The localStorage key used for sync.</param>
    public CrossTabSyncModule(IJSRuntime js, IStore store, string key)
        : base(js, "./crosstabSync.js")
    {
        _store = store;
        _key = key;
        _objRef = DotNetObjectReference.Create(this);
    }

    /// <summary>
    /// Starts listening for cross-tab state changes.
    /// </summary>
    public async Task StartAsync()
    {
        await InvokeVoidAsync(JavaScriptMethods.AddReduxStorageListener, _objRef, _key).ConfigureAwait(false);
    }

    /// <summary>
    /// Invoked by JS when localStorage is updated in another tab.
    /// </summary>
    [JSInvokable]
    public Task OnExternalStateChangedAsync()
    {
        // Optionally debounce/throttle here for frequent changes
        // TODO: Hydrate the store with the new state
        // await _store.HydrateAsync();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        _objRef.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Holds JavaScript export names.
    /// </summary>
    private static class JavaScriptMethods
    {
        public const string AddReduxStorageListener = "addReduxStorageListener";
    }
}
