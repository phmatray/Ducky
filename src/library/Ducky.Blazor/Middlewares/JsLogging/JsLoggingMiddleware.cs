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

    // Configuration options
    private readonly HashSet<string> _excludedActionTypes =
    [
        "StoreInitialized", // System action
        "Tick", // Potentially noisy timer actions
        "Heartbeat" // Health check actions
    ];

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

        // Check if this action type should be excluded
        string actionType = action.GetType().Name;
        if (_excludedActionTypes.Contains(actionType))
        {
            return;
        }

        // Capture previous state and time before action is processed
        _actionMetadata[action] = (_store.CurrentState(), DateTime.Now);
    }

    /// <inheritdoc />
    public override void AfterReduce(object action)
    {
        if (_store is null
            || !_actionMetadata.TryGetValue(action, out (IRootState prevState, DateTime startTime) metadata))
        {
            return;
        }

        try
        {
            (IRootState prevState, DateTime startTime) = metadata;
            double duration = (DateTime.Now - startTime).TotalMilliseconds;
            string timestamp = startTime.ToString("HH:mm:ss.fff");
            IRootState newState = _store.CurrentState();

            // Get state dictionaries
            System.Collections.Immutable.ImmutableSortedDictionary<string, object> prevStateDict = prevState.GetStateDictionary();
            System.Collections.Immutable.ImmutableSortedDictionary<string, object> nextStateDict = newState.GetStateDictionary();

            // Find changed slices
            Dictionary<string, object> changedSlices = [];
            Dictionary<string, object> prevSlices = [];

            foreach (string key in nextStateDict.Keys)
            {
                object? prevValue = prevStateDict.GetValueOrDefault(key);
                object nextValue = nextStateDict[key];

                // Check if the slice changed by comparing JSON representations
                string prevJson = JsonSerializer.Serialize(prevValue);
                string nextJson = JsonSerializer.Serialize(nextValue);

                if (prevJson != nextJson)
                {
                    changedSlices[key] = nextValue;
                    if (prevValue is not null)
                    {
                        prevSlices[key] = prevValue;
                    }
                }
            }

            // Create the label - use uppercase for action types (Redux convention)
            string actionType = action.GetType().Name.ToUpperInvariant();
            string label = $"action {actionType} @ {timestamp} (in {duration:n2} ms)";

            // Determine what to log - show only changed slices if there are any
            object prevToLog;
            object nextToLog;

            if (changedSlices.Count > 0)
            {
                // Only log the slices that changed
                prevToLog = prevSlices;
                nextToLog = changedSlices;
            }
            else
            {
                // No changes detected, log the full state
                prevToLog = prevStateDict;
                nextToLog = nextStateDict;
            }

            JsonElement prevElem = JsonDocument.Parse(JsonSerializer.Serialize(prevToLog)).RootElement;
            JsonElement actionElem = JsonDocument.Parse(JsonSerializer.Serialize(action)).RootElement;
            JsonElement nextElem = JsonDocument.Parse(JsonSerializer.Serialize(nextToLog)).RootElement;

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
