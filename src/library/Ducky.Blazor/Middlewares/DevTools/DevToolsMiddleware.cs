// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Ducky.Pipeline;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Middleware that sends every dispatched action and resulting state to the Redux DevTools browser extension.
/// This middleware integrates with the Redux DevTools extension to provide time-travel debugging capabilities.
/// </summary>
public sealed class DevToolsMiddleware : MiddlewareBase
{
    private readonly ReduxDevToolsModule _devTools;
    private readonly DevToolsOptions _options;
    private readonly DevToolsStateManager _stateManager;
    private readonly ILogger<DevToolsMiddleware> _logger;
    private readonly object _syncRoot = new();
    private readonly List<DevToolsStateEntry> _history = [];
    private IStore? _store;
    private Task _tailTask = Task.CompletedTask;
    private int _sequenceNumberOfCurrentState;
    private int _sequenceNumberOfLatestState;
    private int _committedIndex = -1;
    private bool _isRecording = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevToolsMiddleware"/> class.
    /// </summary>
    /// <param name="devTools">The DevTools JSInterop module.</param>
    /// <param name="stateManager">The state manager for serialization.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The DevTools configuration options.</param>
    public DevToolsMiddleware(
        ReduxDevToolsModule devTools,
        DevToolsStateManager stateManager,
        ILogger<DevToolsMiddleware> logger,
        DevToolsOptions? options = null)
    {
        _devTools = devTools ?? throw new ArgumentNullException(nameof(devTools));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new DevToolsOptions();
    }

    /// <summary>
    /// Gets a value indicating whether recording is active.
    /// </summary>
    internal bool IsRecording => _isRecording;

    /// <summary>
    /// Gets a read-only view of the current history entries.
    /// </summary>
    internal IReadOnlyList<DevToolsStateEntry> History => _history;

    /// <summary>
    /// Gets the committed history index.
    /// </summary>
    internal int CommittedIndex => _committedIndex;

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
        // During time-travel, block user actions but allow HydrateSliceAction and DevTools-internal actions
        if (_sequenceNumberOfCurrentState == _sequenceNumberOfLatestState)
        {
            return true;
        }

        return action is HydrateSliceAction or IDevToolsAction;
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
                .ContinueWith(_ => SendToDevToolsAsync(action, _store, stackTrace))
                .Unwrap();
        }

        // Update sequence numbers for time-travel state management
        _sequenceNumberOfLatestState++;
        _sequenceNumberOfCurrentState = _sequenceNumberOfLatestState;

        // Record history entry when recording is active
        if (!_isRecording || action is IDevToolsAction)
        {
            return;
        }

        RecordHistoryEntry(action);
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
    /// Jumps to a specific action in the history and restores its state.
    /// </summary>
    /// <param name="actionIndex">The index of the action to jump to.</param>
    internal void JumpToAction(int actionIndex)
    {
        if (_store is null)
        {
            return;
        }

        DevToolsStateEntry? entry = _history.Find(e => e.SequenceNumber == actionIndex);
        if (entry is null)
        {
            _logger.LogWarning("DevTools: No history entry found for action index {ActionIndex}", actionIndex);
            return;
        }

        ImmutableSortedDictionary<string, object>? stateDict = _stateManager.DeserializeState(entry.SerializedState);
        if (stateDict is null)
        {
            _logger.LogWarning("DevTools: Failed to deserialize state for action index {ActionIndex}", actionIndex);
            return;
        }

        _store.RestoreState(stateDict);
        _sequenceNumberOfCurrentState = entry.SequenceNumber;

        _logger.LogDebug(
            "DevTools: Jumped to action index {ActionIndex} (current: {CurrentState}, latest: {LatestState})",
            actionIndex,
            _sequenceNumberOfCurrentState,
            _sequenceNumberOfLatestState);
    }

    /// <summary>
    /// Rolls back state to the last committed point and removes history after it.
    /// </summary>
    internal void RollbackToCommitted()
    {
        if (_store is null)
        {
            return;
        }

        if (_committedIndex < 0 || _committedIndex >= _history.Count)
        {
            _logger.LogDebug("DevTools: No committed state to rollback to, restoring initial state");
            ImmutableSortedDictionary<string, object> initialState = _stateManager.GetInitialState();
            _store.RestoreState(initialState);
            _history.Clear();
            _sequenceNumberOfCurrentState = 0;
            _sequenceNumberOfLatestState = 0;
            return;
        }

        DevToolsStateEntry committedEntry = _history[_committedIndex];
        ImmutableSortedDictionary<string, object>? stateDict = _stateManager.DeserializeState(committedEntry.SerializedState);
        if (stateDict is null)
        {
            _logger.LogWarning("DevTools: Failed to deserialize committed state");
            return;
        }

        _store.RestoreState(stateDict);

        // Remove history after the committed point
        if (_committedIndex + 1 < _history.Count)
        {
            _history.RemoveRange(_committedIndex + 1, _history.Count - _committedIndex - 1);
        }

        _sequenceNumberOfCurrentState = committedEntry.SequenceNumber;
        _sequenceNumberOfLatestState = committedEntry.SequenceNumber;

        _logger.LogDebug("DevTools: Rolled back to committed state at index {Index}", _committedIndex);
    }

    /// <summary>
    /// Removes all skipped entries from history and replays non-skipped actions from initial state.
    /// </summary>
    internal void SweepSkippedActions()
    {
        if (_store is null)
        {
            return;
        }

        // Remove skipped entries
        int removedCount = _history.RemoveAll(e => e.IsSkipped);
        if (removedCount == 0)
        {
            _logger.LogDebug("DevTools: No skipped actions to sweep");
            return;
        }

        // Replay from initial state
        ReplayNonSkippedActions();

        _logger.LogDebug("DevTools: Swept {Count} skipped actions", removedCount);
    }

    /// <summary>
    /// Toggles the skip flag on a specific action and replays from initial state.
    /// </summary>
    /// <param name="actionIndex">The index of the action to toggle.</param>
    internal void ToggleAction(int actionIndex)
    {
        if (_store is null)
        {
            return;
        }

        int historyIdx = _history.FindIndex(e => e.SequenceNumber == actionIndex);
        if (historyIdx < 0)
        {
            _logger.LogWarning("DevTools: No history entry found for action index {ActionIndex}", actionIndex);
            return;
        }

        _history[historyIdx] = _history[historyIdx].WithToggledSkip();

        // Replay from initial state with the toggled skip state
        ReplayNonSkippedActions();

        _logger.LogDebug("DevTools: Toggled action at index {ActionIndex}", actionIndex);
    }

    /// <summary>
    /// Sets the committed index to the current end of history.
    /// </summary>
    internal void Commit()
    {
        _committedIndex = _history.Count - 1;
        _logger.LogDebug("DevTools: Committed state at index {Index}", _committedIndex);
    }

    /// <summary>
    /// Controls whether action recording is active.
    /// </summary>
    /// <param name="isPaused">Whether recording should be paused.</param>
    internal void SetRecording(bool isPaused)
    {
        _isRecording = !isPaused;
        _logger.LogDebug("DevTools: Recording {Status}", _isRecording ? "resumed" : "paused");
    }

    /// <summary>
    /// Resets the sequence tracking after a commit operation.
    /// </summary>
    internal async Task OnCommitAsync()
    {
        // Wait for pending DevTools notifications to complete
        await _tailTask.ConfigureAwait(false);

        // Commit the current state
        Commit();

        // Reset sequence tracking
        _sequenceNumberOfCurrentState = 0;
        _sequenceNumberOfLatestState = 0;

        // Reinitialize DevTools with current state
        if (_store is null)
        {
            return;
        }

        await _devTools.InitAsync(_store).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles jumping to a specific action in the DevTools history.
    /// </summary>
    /// <param name="actionIndex">The index of the action to jump to.</param>
    internal Task OnJumpToStateAsync(int actionIndex)
    {
        JumpToAction(actionIndex);
        return Task.CompletedTask;
    }

    private void RecordHistoryEntry(object action)
    {
        if (_store is null)
        {
            return;
        }

        string serializedState = _stateManager.SerializeState(_store);
        DevToolsStateEntry entry = new(
            _sequenceNumberOfLatestState,
            action,
            serializedState,
            IsSkipped: false,
            DateTime.UtcNow);

        _history.Add(entry);

        // Enforce MaxAge cap
        if (_history.Count <= _options.MaxAge)
        {
            return;
        }

        int excess = _history.Count - _options.MaxAge;
        _history.RemoveRange(0, excess);

        // Adjust committed index
        _committedIndex = Math.Max(-1, _committedIndex - excess);
    }

    private void ReplayNonSkippedActions()
    {
        if (_store is null)
        {
            return;
        }

        // Restore initial state
        ImmutableSortedDictionary<string, object> initialState = _stateManager.GetInitialState();
        _store.RestoreState(initialState);

        // Re-dispatch each non-skipped action's original action object
        foreach (DevToolsStateEntry entry in _history)
        {
            if (!entry.IsSkipped)
            {
#pragma warning disable CS0618
                _store.RestoreState(
                    _stateManager.DeserializeState(entry.SerializedState)
                    ?? _stateManager.GetInitialState());
#pragma warning restore CS0618
            }
        }

        // Update sequence numbers
        if (_history.Count > 0)
        {
            DevToolsStateEntry lastNonSkipped = _history.FindLast(e => !e.IsSkipped)
                                                 ?? _history[^1];
            _sequenceNumberOfCurrentState = lastNonSkipped.SequenceNumber;
            _sequenceNumberOfLatestState = lastNonSkipped.SequenceNumber;
        }
        else
        {
            _sequenceNumberOfCurrentState = 0;
            _sequenceNumberOfLatestState = 0;
        }
    }

    private async Task SendToDevToolsAsync(object action, IStateProvider stateProvider, string? stackTrace)
    {
        try
        {
            // Convert IStateProvider to its dictionary representation for DevTools
            object stateDict = stateProvider.GetAllSlices();
            await _devTools.SendAsync(action, stateDict, stackTrace).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log but don't crash the application
            _logger.LogWarning(ex, "Failed to send to DevTools");
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
