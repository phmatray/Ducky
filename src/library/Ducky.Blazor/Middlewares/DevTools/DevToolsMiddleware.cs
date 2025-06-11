using System.Diagnostics;
using Ducky.Pipeline;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Middleware that sends every dispatched action and resulting state to the Redux DevTools browser extension.
/// This middleware integrates with the Redux DevTools extension to provide time-travel debugging capabilities.
/// </summary>
public sealed class DevToolsMiddleware : MiddlewareBase
{
    private readonly ReduxDevToolsModule _devTools;
    private readonly DevToolsOptions _options;
    private readonly object _syncRoot = new();
    private IStore? _store;
    private Task _tailTask = Task.CompletedTask;
    private int _sequenceNumberOfCurrentState = 0;
    private int _sequenceNumberOfLatestState = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevToolsMiddleware"/> class.
    /// </summary>
    /// <param name="devTools">The DevTools JSInterop module.</param>
    /// <param name="options">The DevTools configuration options.</param>
    public DevToolsMiddleware(ReduxDevToolsModule devTools, DevToolsOptions? options = null)
    {
        _devTools = devTools ?? throw new ArgumentNullException(nameof(devTools));
        _options = options ?? new DevToolsOptions();
    }

    /// <inheritdoc />
    public override Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _store = store;

        // Set store and dispatcher on DevTools module
        _devTools.SetStoreAndDispatcher(store, dispatcher);

        // Set up callbacks for DevTools operations
        _devTools.SetOnCommitCallback(OnCommitAsync);
        _devTools.SetOnJumpToStateCallback(OnJumpToStateAsync);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override bool MayDispatchAction(object action)
    {
        // Only allow dispatching if we're viewing the latest state (not in a historical state)
        return _sequenceNumberOfCurrentState == _sequenceNumberOfLatestState;
    }

    /// <inheritdoc />
    public override void AfterReduce(object action)
    {
        if (_store is null)
        {
            return;
        }

        // Check if action should be logged
        if (!ShouldLogAction(action))
        {
            return;
        }

        // Capture stack trace if enabled
        string? stackTrace = null;
        if (_options.StackTraceEnabled)
        {
            stackTrace = CaptureStackTrace();
        }

        // Queue the DevTools update to ensure sequential processing
        lock (_syncRoot)
        {
            _tailTask = _tailTask
                .ContinueWith(_ => SendToDevToolsAsync(action, _store.CurrentState, stackTrace))
                .Unwrap();
        }

        // Update sequence numbers for time-travel state management
        _sequenceNumberOfLatestState++;
        _sequenceNumberOfCurrentState = _sequenceNumberOfLatestState;
    }

    /// <summary>
    /// Sets the current state sequence number for time-travel debugging.
    /// </summary>
    /// <param name="sequenceNumber">The sequence number to jump to.</param>
    internal void SetCurrentSequenceNumber(int sequenceNumber)
    {
        _sequenceNumberOfCurrentState = sequenceNumber;
    }

    /// <summary>
    /// Resets the sequence tracking after a commit operation.
    /// </summary>
    internal async Task OnCommitAsync()
    {
        // Wait for pending DevTools notifications to complete
        await _tailTask.ConfigureAwait(false);

        // Reset sequence tracking
        _sequenceNumberOfCurrentState = 0;
        _sequenceNumberOfLatestState = 0;

        // Reinitialize DevTools with current state
        if (_store is null)
        {
            return;
        }

        await _devTools.InitAsync(_store.CurrentState).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles jumping to a specific action in the DevTools history.
    /// </summary>
    /// <param name="actionIndex">The index of the action to jump to.</param>
    internal Task OnJumpToStateAsync(int actionIndex)
    {
        // Update the current sequence number to reflect the jump
        _sequenceNumberOfCurrentState = actionIndex;

        // Log the jump operation for debugging
        Console.WriteLine(
            $"DevTools: Jumped to action index {actionIndex} "
                + $"(current: {_sequenceNumberOfCurrentState}, latest: {_sequenceNumberOfLatestState})");

        // Note: The actual state restoration is handled by the DevTools module
        // which will dispatch appropriate actions through the normal pipeline

        return Task.CompletedTask;
    }

    private async Task SendToDevToolsAsync(object action, IRootState state, string? stackTrace)
    {
        try
        {
            // Convert IRootState to its dictionary representation for DevTools
            object stateDict = state.GetStateDictionary();
            await _devTools.SendAsync(action, stateDict, stackTrace).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log but don't crash the application
            Console.WriteLine($"Failed to send to DevTools: {ex.Message}");
        }
    }

    private bool ShouldLogAction(object action)
    {
        // Check if action type is excluded
        string actionType = action.GetType().Name;
        return !_options.ExcludedActionTypes.Contains(actionType, StringComparer.OrdinalIgnoreCase);
    }

    private string CaptureStackTrace()
    {
        StackTrace stackTrace = new(fNeedFileInfo: true);
        int maxFrames = _options.StackTraceLimit == 0 ? int.MaxValue : _options.StackTraceLimit;

        IEnumerable<string> frames = stackTrace.GetFrames()
            .Select(frame =>
            {
                System.Reflection.MethodBase? method = frame.GetMethod();
                if (method is null)
                {
                    return null;
                }

                string? fileName = frame.GetFileName();
                int lineNumber = frame.GetFileLineNumber();
                int columnNumber = frame.GetFileColumnNumber();

                return $"at {method.DeclaringType?.FullName}.{method.Name} ({fileName}:{lineNumber}:{columnNumber})";
            })
            .Where(x => x is not null)
            .Cast<string>();

        // Apply regex filter if configured
        if (_options.StackTraceFilterRegex is not null)
        {
            frames = frames.Where(x => _options.StackTraceFilterRegex.IsMatch(x));
        }

        return string.Join("\r\n", frames.Take(maxFrames));
    }
}
