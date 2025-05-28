using System.Text.Json;
using Ducky.Pipeline;
using R3;

namespace Ducky.Blazor.Middlewares.JsLogging;

/// <summary>
/// Reactive middleware that logs actions and state changes to the browser console.
/// </summary>
public sealed class JsLoggingMiddleware : IActionMiddleware
{
    private readonly JsConsoleLoggerModule _loggerModule;
    private readonly Func<IRootState> _getState;

    private const string MetadataPrevState = "PrevState";
    private const string MetadataStartTime = "StartTime";

    /// <summary>
    /// Initializes a new instance of the <see cref="JsLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="loggerModule">The logger module for console logging.</param>
    /// <param name="getState">A function to get the current root state.</param>
    public JsLoggingMiddleware(JsConsoleLoggerModule loggerModule, Func<IRootState> getState)
    {
        _loggerModule = loggerModule;
        _getState = getState;
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        // Attach previous state and time to context metadata
        return actions.Do(ctx =>
        {
            ctx.SetMetadata(MetadataPrevState, _getState());
            ctx.SetMetadata(MetadataStartTime, DateTime.Now);
        });
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        return actions.Do(ctx =>
        {
            object? prevState = ctx.TryGetMetadata(MetadataPrevState, out object? prev) ? prev : null;
            DateTime startTime = ctx.TryGetMetadata(MetadataStartTime, out DateTime st) ? st : DateTime.Now;
            double duration = (DateTime.Now - startTime).TotalMilliseconds;
            string timestamp = startTime.ToString("HH:mm:ss.fff");
            string label = $"action {ctx.Action.GetType().Name} @ {timestamp} (in {duration:n2} ms)";
            IRootState newState = _getState();

            JsonElement prevElem = JsonDocument.Parse(JsonSerializer.Serialize(prevState)).RootElement;
            JsonElement actionElem = JsonDocument.Parse(JsonSerializer.Serialize(ctx.Action)).RootElement;
            JsonElement nextElem = JsonDocument.Parse(JsonSerializer.Serialize(newState)).RootElement;

            // Fire-and-forget async logging
            _ = _loggerModule.LogAsync(label, prevElem, actionElem, nextElem);
        });
    }
}
