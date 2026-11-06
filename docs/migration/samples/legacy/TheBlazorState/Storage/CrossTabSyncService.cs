using Microsoft.JSInterop;

namespace TheBlazorState.Storage;

/// <summary>
/// Scoped service that listens for localStorage changes from other browser tabs
/// and propagates them to the Blazor circuit.
/// </summary>
public sealed class CrossTabSyncService : IAsyncDisposable, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<string, Action<string>> _callbacks = new();
    private IJSObjectReference? _module;
    private DotNetObjectReference<CrossTabSyncService>? _dotNetRef;
    private bool _listening;

    public CrossTabSyncService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Register a callback for when a localStorage key changes in another tab.
    /// </summary>
    public void RegisterKey(string key, Action<string> onChanged)
    {
        _callbacks[key] = onChanged;
    }

    /// <summary>
    /// Start listening for cross-tab storage events. Called lazily on first use.
    /// Must be called after the circuit is interactive (not during prerender).
    /// </summary>
    public async Task StartListeningAsync()
    {
        if (_listening || _callbacks.Count == 0) return;
        _listening = true; // Set early to prevent concurrent calls

        try
        {
            _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/TheBlazorState/theblazorstate.js");

            _dotNetRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("registerCrossTabSync", _dotNetRef);
        }
        catch (InvalidOperationException)
        {
            // JSInterop not available (prerender) — will retry later
            _listening = false;
        }
    }

    /// <summary>
    /// Event fired after a cross-tab change has been processed.
    /// Components can subscribe to this to schedule re-renders.
    /// </summary>
    public event Action? AfterCrossTabChange;

    [JSInvokable]
    public void OnStorageChanged(string key, string rawJson)
    {
        if (_callbacks.TryGetValue(key, out var callback))
        {
            callback(rawJson);
            AfterCrossTabChange?.Invoke();
        }
    }

    public void Dispose()
    {
        _dotNetRef?.Dispose();
        _callbacks.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_listening && _module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unregisterCrossTabSync");
            }
            catch
            {
                // Circuit may already be gone
            }
        }

        _dotNetRef?.Dispose();
        _callbacks.Clear();
    }
}
