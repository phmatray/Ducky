// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Special reducer that handles DevTools actions for state restoration.
/// This reducer bypasses normal slice reducers and directly manipulates the root state.
/// </summary>
public class DevToolsReducer
{
    private readonly DevToolsStateManager _stateManager;
    private readonly DevToolsMiddleware _middleware;
    private readonly ILogger<DevToolsReducer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevToolsReducer"/> class.
    /// </summary>
    /// <param name="stateManager">The state manager for serialization/deserialization.</param>
    /// <param name="middleware">The DevTools middleware that owns history state.</param>
    /// <param name="logger">The logger instance.</param>
    public DevToolsReducer(DevToolsStateManager stateManager, DevToolsMiddleware middleware, ILogger<DevToolsReducer> logger)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _middleware = middleware ?? throw new ArgumentNullException(nameof(middleware));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reduces DevTools actions to create new root state.
    /// </summary>
    /// <param name="currentState">The current root state.</param>
    /// <param name="action">The action to process.</param>
    /// <returns>New root state or the current state if action is not handled.</returns>
    public IStateProvider Reduce(IStateProvider currentState, object action)
    {
        return action switch
        {
            DevToolsActions.RestoreState restoreAction => CreateRootState(restoreAction.State),
            DevToolsActions.ResetToInitial => CreateRootState(_stateManager.GetInitialState()),
            DevToolsActions.JumpToAction jumpAction => HandleJumpToAction(currentState, jumpAction),
            DevToolsActions.ReplayActions replayAction => HandleReplayActions(currentState, replayAction),
            DevToolsActions.CommitState => HandleCommitState(currentState),
            DevToolsActions.RollbackToCommitted => HandleRollbackToCommitted(currentState),
            DevToolsActions.SweepSkippedActions => HandleSweepSkippedActions(currentState),
            DevToolsActions.ToggleAction toggleAction => HandleToggleAction(currentState, toggleAction),
            DevToolsActions.RecordingControl recordingAction => HandleRecordingControl(currentState, recordingAction),
            _ => currentState
        };
    }

    /// <summary>
    /// Determines if an action should be handled by the DevTools reducer.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <returns>True if the action is a DevTools action; otherwise, false.</returns>
    public static bool IsDevToolsAction(object action)
    {
        return action is IDevToolsAction;
    }

    /// <summary>
    /// Creates a new root state from a state dictionary.
    /// </summary>
    /// <param name="stateDict">The state dictionary.</param>
    /// <returns>A new root state instance.</returns>
    private static IStateProvider CreateRootState(ImmutableSortedDictionary<string, object> stateDict)
    {
        return new RootState(stateDict);
    }

    /// <summary>
    /// Handles jump to action operations by delegating to the middleware.
    /// </summary>
    private IStateProvider HandleJumpToAction(IStateProvider currentState, DevToolsActions.JumpToAction jumpAction)
    {
        _logger.LogDebug("DevTools: Jump to action {ActionIndex} ({ActionType})", jumpAction.ActionIndex, jumpAction.ActionType);
        _middleware.JumpToAction(jumpAction.ActionIndex);
        return currentState;
    }

    /// <summary>
    /// Handles replay actions operations by jumping to the target action index.
    /// </summary>
    private IStateProvider HandleReplayActions(IStateProvider currentState, DevToolsActions.ReplayActions replayAction)
    {
        _logger.LogDebug(
            "DevTools: Replay actions from {FromIndex} to {ToIndex}",
            replayAction.FromActionIndex,
            replayAction.ToActionIndex);

        _middleware.JumpToAction(replayAction.ToActionIndex);
        return currentState;
    }

    /// <summary>
    /// Handles commit state operations by delegating to the middleware.
    /// </summary>
    private IStateProvider HandleCommitState(IStateProvider currentState)
    {
        _logger.LogDebug("DevTools: Commit state handled in reducer");
        _middleware.Commit();
        return currentState;
    }

    /// <summary>
    /// Handles rollback to committed state by delegating to the middleware.
    /// </summary>
    private IStateProvider HandleRollbackToCommitted(IStateProvider currentState)
    {
        _logger.LogDebug("DevTools: Rollback to committed state");
        _middleware.RollbackToCommitted();
        return currentState;
    }

    /// <summary>
    /// Handles sweep skipped actions by delegating to the middleware.
    /// </summary>
    private IStateProvider HandleSweepSkippedActions(IStateProvider currentState)
    {
        _logger.LogDebug("DevTools: Sweep skipped actions");
        _middleware.SweepSkippedActions();
        return currentState;
    }

    /// <summary>
    /// Handles toggle action by delegating to the middleware.
    /// </summary>
    private IStateProvider HandleToggleAction(IStateProvider currentState, DevToolsActions.ToggleAction toggleAction)
    {
        _logger.LogDebug("DevTools: Toggle action at index {ActionIndex}", toggleAction.ActionIndex);
        _middleware.ToggleAction(toggleAction.ActionIndex);
        return currentState;
    }

    /// <summary>
    /// Handles recording control by delegating to the middleware.
    /// </summary>
    private IStateProvider HandleRecordingControl(IStateProvider currentState, DevToolsActions.RecordingControl recordingAction)
    {
        string status = recordingAction.IsPaused ? "paused" : recordingAction.IsLocked ? "locked" : "resumed";
        _logger.LogDebug("DevTools: Recording {Status}", status);
        _middleware.SetRecording(recordingAction.IsPaused);
        return currentState;
    }
}
