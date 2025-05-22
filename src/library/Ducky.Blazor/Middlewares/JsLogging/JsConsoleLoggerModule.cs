using System.Text.Json;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Middlewares.JsLogging;

/// <summary>
/// Provides methods to log to the browser console via JSInterop.
/// </summary>
public sealed class JsConsoleLoggerModule : JsModule
{
    /// <summary>
    /// Create a new JS Console logger module.
    /// </summary>
    /// <param name="js">The Blazor JS runtime.</param>
    public JsConsoleLoggerModule(IJSRuntime js)
        : base(js, "./jsConsoleLogger.js") // Replace with your JS module path
    {
    }

    /// <summary>
    /// Logs a state/action/state group to the browser's developer console.
    /// </summary>
    /// <param name="label">Label for the console group.</param>
    /// <param name="prevState">State before the action.</param>
    /// <param name="action">The dispatched action.</param>
    /// <param name="nextState">State after the action.</param>
    public async Task LogAsync(string label, JsonElement prevState, JsonElement action, JsonElement nextState)
    {
        await InvokeVoidAsync("logGroup", label, prevState, action, nextState).ConfigureAwait(false);
    }
}
