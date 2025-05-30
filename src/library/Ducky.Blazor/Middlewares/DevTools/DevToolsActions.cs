using System.Collections.Immutable;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Internal actions used by the DevTools middleware for state management.
/// These actions are not intended for use by application code.
/// </summary>
internal static class DevToolsActions
{
    /// <summary>
    /// Action to restore the application state from DevTools time-travel.
    /// This action bypasses normal reducers and directly sets the state.
    /// </summary>
    /// <param name="State">The state dictionary to restore.</param>
    /// <param name="ActionIndex">The action index in DevTools history.</param>
    /// <param name="Timestamp">When the restoration was requested.</param>
    internal record RestoreState(
        ImmutableSortedDictionary<string, object> State,
        int ActionIndex,
        DateTime Timestamp) : IDevToolsAction;

    /// <summary>
    /// Action to jump to a specific action in the DevTools timeline.
    /// </summary>
    /// <param name="ActionIndex">The action index to jump to.</param>
    /// <param name="ActionType">The type of the action being jumped to.</param>
    /// <param name="Timestamp">When the jump was requested.</param>
    internal record JumpToAction(
        int ActionIndex,
        string ActionType,
        DateTime Timestamp) : IDevToolsAction;

    /// <summary>
    /// Action to reset the application to its initial state.
    /// </summary>
    /// <param name="Timestamp">When the reset was requested.</param>
    internal record ResetToInitial(DateTime Timestamp) : IDevToolsAction;

    /// <summary>
    /// Action to replay actions from a specific point.
    /// </summary>
    /// <param name="FromActionIndex">Starting action index for replay.</param>
    /// <param name="ToActionIndex">Ending action index for replay.</param>
    /// <param name="Timestamp">When the replay was requested.</param>
    internal record ReplayActions(
        int FromActionIndex,
        int ToActionIndex,
        DateTime Timestamp) : IDevToolsAction;

    /// <summary>
    /// Action to commit the current state as the new baseline.
    /// </summary>
    /// <param name="Timestamp">When the commit was requested.</param>
    internal record CommitState(DateTime Timestamp) : IDevToolsAction;

    /// <summary>
    /// Action to rollback to the last committed state.
    /// </summary>
    /// <param name="Timestamp">When the rollback was requested.</param>
    internal record RollbackToCommitted(DateTime Timestamp) : IDevToolsAction;

    /// <summary>
    /// Action to sweep (remove) all skipped actions from history.
    /// </summary>
    /// <param name="Timestamp">When the sweep was requested.</param>
    internal record SweepSkippedActions(DateTime Timestamp) : IDevToolsAction;

    /// <summary>
    /// Action to toggle (skip/unskip) a specific action.
    /// </summary>
    /// <param name="ActionIndex">The index of the action to toggle.</param>
    /// <param name="Timestamp">When the toggle was requested.</param>
    internal record ToggleAction(int ActionIndex, DateTime Timestamp) : IDevToolsAction;

    /// <summary>
    /// Action to control recording (pause/resume/lock).
    /// </summary>
    /// <param name="IsPaused">Whether recording is paused.</param>
    /// <param name="IsLocked">Whether changes are locked.</param>
    /// <param name="Timestamp">When the control change was requested.</param>
    internal record RecordingControl(bool IsPaused, bool IsLocked, DateTime Timestamp) : IDevToolsAction;
}

/// <summary>
/// Marker interface for DevTools internal actions.
/// </summary>
internal interface IDevToolsAction
{
}
