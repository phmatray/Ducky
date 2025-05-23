using System.Text.Json;
using Ducky.Middlewares;
using Ducky.Pipeline;

namespace Ducky.Blazor.Middlewares.JsLogging;

/// <summary>
/// Middleware that logs the action and state changes to the console.
/// </summary>
/// <typeparam name="TState"></typeparam>
public sealed class JsLoggingMiddleware<TState> : StoreMiddleware
    where TState : class
{
    private readonly JsConsoleLoggerModule _loggerModule;
    private IStore? _store;

    private const string MetadataPrevState = "PrevState";
    private const string MetadataStartTime = "StartTime";

    /// <summary>
    /// Initializes a new instance of the <see cref="JsLoggingMiddleware{TState}"/> class.
    /// </summary>
    /// <param name="loggerModule">The logger module to use for logging.</param>
    public JsLoggingMiddleware(JsConsoleLoggerModule loggerModule)
    {
        _loggerModule = loggerModule;
    }

    /// <inheritdoc />
    public override async Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override StoreMiddlewareAsyncMode AsyncMode
        => StoreMiddlewareAsyncMode.FireAndForget;

    /// <inheritdoc />
    public override async Task BeforeDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        context.SetMetadata(MetadataPrevState, _store!.CurrentState);
        context.SetMetadata(MetadataStartTime, DateTime.Now);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task AfterDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        TState? prevState = context.TryGetMetadata(MetadataPrevState, out TState? prev) ? prev : null;
        DateTime startTime = context.TryGetMetadata(MetadataStartTime, out DateTime st) ? st : DateTime.Now;
        double duration = (DateTime.Now - startTime).TotalMilliseconds;
        string timestamp = startTime.ToString("HH:mm:ss.fff");
        string label = $"action {typeof(TAction).Name} @ {timestamp} (in {duration:n2} ms)";
        IRootState newState = _store!.CurrentState;

        JsonElement prevElem = JsonDocument.Parse(JsonSerializer.Serialize(prevState)).RootElement;
        JsonElement actionElem = JsonDocument.Parse(JsonSerializer.Serialize(context.Action)).RootElement;
        JsonElement nextElem = JsonDocument.Parse(JsonSerializer.Serialize(newState)).RootElement;

        // Use logger module, fire-and-forget
        _ = _loggerModule.LogAsync(label, prevElem, actionElem, nextElem);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
