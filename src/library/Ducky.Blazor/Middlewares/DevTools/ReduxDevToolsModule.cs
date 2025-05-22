using System.Text.Json;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Provides a .NET wrapper for the Redux DevTools browser extension via JSInterop.
/// Handles sending state and actions to DevTools, and applies time-travel state updates.
/// </summary>
/// <typeparam name="TState">Type of your Redux state object.</typeparam>
public class ReduxDevToolsModule<TState> : JsModule
    where TState : class
{
    private readonly Action<TState> _setState;
    private string _storeName;
    private bool _enabled;
    private readonly TaskCompletionSource<bool> _readyTcs = new();

    /// <summary>
    /// Completes when the DevTools extension is ready.
    /// </summary>
    public Task WhenReady => _readyTcs.Task;

    /// <summary>
    /// Create a new DevToolsInterop binding.
    /// </summary>
    /// <param name="js">The Blazor JS runtime.</param>
    /// <param name="setState">Delegate used to set state when time-travel occurs.</param>
    /// <param name="storeName">The label for this store in DevTools UI.</param>
    public ReduxDevToolsModule(
        IJSRuntime js,
        Action<TState> setState,
        string? storeName = null)
        : base(js, "./reduxDevtools.js")
    {
        _setState = setState;
        _storeName = storeName ?? typeof(TState).Name;
    }

    /// <summary>
    /// Sets the store name for the DevTools connection.
    /// </summary>
    /// <param name="storeName">The name of the store.</param>
    public void SetStoreName(string? storeName)
    {
        _storeName = storeName ?? typeof(TState).Name;
    }

    /// <summary>
    /// Initializes the DevTools connection and dispatches @@INIT.
    /// </summary>
    /// <param name="initialState">Initial state of the Redux store.</param>
    public async Task InitAsync(TState initialState)
    {
        _enabled = await InvokeAsync<bool>(JavaScriptMethods.InitDevTools, _storeName)
            .ConfigureAwait(false);

        // Dispatch the @@INIT action with initial state
        if (_enabled)
        {
            await InvokeVoidAsync(JavaScriptMethods.SendToDevTools, new { type = "@@INIT" }, initialState)
                .ConfigureAwait(false);
        }

        _readyTcs.TrySetResult(true);
    }

    /// <summary>
    /// Sends an action and the resulting state to the DevTools extension.
    /// </summary>
    /// <param name="action">The dispatched action (for type labeling).</param>
    /// <param name="state">The state after the action.</param>
    public async Task SendAsync(object action, object state)
    {
        if (!_enabled)
        {
            return;
        }

        var actionObj = new { type = action.GetType().Name };
        await InvokeVoidAsync(JavaScriptMethods.SendToDevTools, actionObj, state).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends an action and the resulting state to the DevTools extension.
    /// </summary>
    /// <param name="actionType">Action type (string).</param>
    /// <param name="state">The state after the action.</param>
    public async Task SendAsync(string actionType, TState state)
    {
        if (!_enabled)
        {
            return;
        }

        await InvokeVoidAsync(JavaScriptMethods.SendToDevTools, new { type = actionType }, state)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Subscribes to Redux DevTools messages (for time-travel support).
    /// </summary>
    public async Task SubscribeAsync()
    {
        if (!_enabled)
        {
            return;
        }

        DotNetObjectReference<ReduxDevToolsModule<TState>> dotNetRef = DotNetObjectReference.Create(this);
        await InvokeVoidAsync(JavaScriptMethods.SubscribeToDevTools, dotNetRef).ConfigureAwait(false);
    }

    /// <summary>
    /// Invoked from JS when DevTools requests a state change (time-travel).
    /// </summary>
    /// <param name="jsonState">State as JSON (serialized from JS).</param>
    [JSInvokable]
    public Task OnDevToolsStateAsync(string jsonState)
    {
        TState? state = JsonSerializer.Deserialize<TState>(jsonState);

        if (state is not null)
        {
            _setState(state);
        }

        return Task.CompletedTask;
    }

    private static class JavaScriptMethods
    {
        public const string InitDevTools = "initDevTools";
        public const string SendToDevTools = "sendToDevTools";
        public const string SubscribeToDevTools = "subscribeToDevTools";
    }
}
