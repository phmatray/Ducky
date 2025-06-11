using System.Text.Json;
using Ducky.Pipeline;

namespace Ducky.Blazor.Middlewares.JsLogging;

/// <summary>
/// Middleware that logs actions and state changes to the browser console.
/// </summary>
public sealed class JsLoggingMiddleware : MiddlewareBase
{
    private readonly JsConsoleLoggerModule _loggerModule;
    private IStore? _store;
    private readonly Dictionary<object, (IRootState prevState, DateTime startTime)> _actionMetadata = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="JsLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="loggerModule">The logger module for console logging.</param>
    public JsLoggingMiddleware(JsConsoleLoggerModule loggerModule)
    {
        _loggerModule = loggerModule ?? throw new ArgumentNullException(nameof(loggerModule));
    }

    /// <inheritdoc />
    public override Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _store = store;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void BeforeReduce(object action)
    {
        if (_store is null)
        {
            return;
        }

        // Capture previous state and time before action is processed
        _actionMetadata[action] = (_store.CurrentState, DateTime.Now);
    }

    /// <inheritdoc />
    public override void AfterReduce(object action)
    {
        if (_store is null || !_actionMetadata.TryGetValue(action, out (IRootState prevState, DateTime startTime) metadata))
        {
            return;
        }

        try
        {
            (IRootState prevState, DateTime startTime) = metadata;
            double duration = (DateTime.Now - startTime).TotalMilliseconds;
            string timestamp = startTime.ToString("HH:mm:ss.fff");
            string label = $"action {action.GetType().Name} @ {timestamp} (in {duration:n2} ms)";
            IRootState newState = _store.CurrentState;

            JsonElement prevElem = JsonDocument.Parse(JsonSerializer.Serialize(prevState)).RootElement;
            JsonElement actionElem = JsonDocument.Parse(JsonSerializer.Serialize(action)).RootElement;
            JsonElement nextElem = JsonDocument.Parse(JsonSerializer.Serialize(newState)).RootElement;

            // Fire-and-forget async logging
            _ = _loggerModule.LogAsync(label, prevElem, actionElem, nextElem);
        }
        finally
        {
            // Clean up metadata
            _actionMetadata.Remove(action);
        }
    }
}
