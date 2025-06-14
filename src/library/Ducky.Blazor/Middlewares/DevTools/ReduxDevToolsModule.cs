using System.Collections.Immutable;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Provides a .NET wrapper for the Redux DevTools browser extension via JSInterop.
/// Handles sending state and actions to DevTools, and applies time-travel state updates.
/// </summary>
public class ReduxDevToolsModule : JsModule
{
    private IStore? _store;
    private IDispatcher? _dispatcher;
    private readonly DevToolsOptions _options;
    private readonly DevToolsStateManager _stateManager;
    private bool _enabled;
    private readonly TaskCompletionSource<bool> _readyTcs = new();

    // Callbacks for DevTools operations
    private Func<Task>? _onCommit;
    private Func<int, Task>? _onJumpToState;

    /// <summary>
    /// Completes when the DevTools extension is ready.
    /// </summary>
    public Task WhenReady => _readyTcs.Task;

    /// <summary>
    /// Gets a value indicating whether DevTools is enabled and ready.
    /// </summary>
    public bool IsEnabled => _enabled;

    /// <summary>
    /// Gets the current DevTools configuration options.
    /// </summary>
    public DevToolsOptions Options => _options;

    /// <summary>
    /// Gets the state manager for serialization operations.
    /// </summary>
    public DevToolsStateManager StateManager => _stateManager;

    /// <summary>
    /// Create a new DevToolsInterop binding.
    /// </summary>
    /// <param name="js">The Blazor JS runtime.</param>
    /// <param name="stateManager">The state manager for serialization.</param>
    /// <param name="options">Configuration options for DevTools.</param>
    public ReduxDevToolsModule(
        IJSRuntime js,
        DevToolsStateManager stateManager,
        DevToolsOptions? options = default)
        : base(js, "./_content/Ducky.Blazor/reduxDevtools.js")
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _options = options ?? new DevToolsOptions();
    }

    /// <summary>
    /// Sets the store and dispatcher instances. Must be called before other operations.
    /// </summary>
    /// <param name="store">The Ducky store instance.</param>
    /// <param name="dispatcher">The dispatcher instance.</param>
    public void SetStoreAndDispatcher(IStore store, IDispatcher dispatcher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Sets the callback for DevTools commit operations.
    /// </summary>
    /// <param name="onCommit">The callback to invoke when DevTools requests a commit.</param>
    public void SetOnCommitCallback(Func<Task> onCommit)
    {
        _onCommit = onCommit;
    }

    /// <summary>
    /// Sets the callback for DevTools jump to state operations.
    /// </summary>
    /// <param name="onJumpToState">The callback to invoke when DevTools jumps to a state.</param>
    public void SetOnJumpToStateCallback(Func<int, Task> onJumpToState)
    {
        _onJumpToState = onJumpToState;
    }

    /// <summary>
    /// Initializes the DevTools connection and dispatches @@INIT.
    /// </summary>
    /// <param name="initialState">Initial state of the Redux store.</param>
    public async Task InitAsync(IRootState? initialState = null)
    {
        // Don't initialize if disabled in configuration
        if (!_options.Enabled)
        {
            _enabled = false;
            _readyTcs.TrySetResult(false);
            return;
        }

        try
        {
            _enabled = await InvokeAsync<bool>(JavaScriptMethods.InitDevTools, _options.StoreName)
                .ConfigureAwait(false);

            // Dispatch the @@INIT action with initial state
            if (_enabled && _store is not null)
            {
                IRootState state = initialState ?? _store.CurrentState();

                // Store initial state for reset operations
                ImmutableSortedDictionary<string, object> stateDict = state.GetStateDictionary();
                _stateManager.SetInitialState(stateDict);

                await InvokeVoidAsync(
                    JavaScriptMethods.SendToDevTools,
                    new { type = "@@INIT" },
                    stateDict)
                    .ConfigureAwait(false);

                // Subscribe to DevTools messages for time-travel if enabled
                if (_options.EnableTimeTravel)
                {
                    await SubscribeAsync().ConfigureAwait(false);
                }
            }

            _readyTcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            // DevTools initialization failed, but don't crash the application
            _enabled = false;
            _readyTcs.TrySetResult(false);

            // Log the error if a logger is available
            // This is optional and should not fail
            try
            {
                Console.WriteLine($"DevTools initialization failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    /// <summary>
    /// Sends an action and the resulting state to the DevTools extension.
    /// </summary>
    /// <param name="action">The dispatched action (for type labeling).</param>
    /// <param name="state">The state after the action.</param>
    /// <param name="stackTrace">Optional stack trace for debugging.</param>
    public async Task SendAsync(object action, object state, string? stackTrace = null)
    {
        if (!_enabled || !ShouldLogAction(action))
        {
            return;
        }

        try
        {
            // Create enhanced action object with metadata
            object actionObj = CreateActionObject(action, stackTrace);
            await InvokeVoidAsync(JavaScriptMethods.SendToDevTools, actionObj, state)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log but don't crash the application
            try
            {
                Console.WriteLine($"DevTools send failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    /// <summary>
    /// Sends an action and the resulting state to the DevTools extension.
    /// </summary>
    /// <param name="actionType">Action type (string).</param>
    /// <param name="state">The state after the action.</param>
    public async Task SendAsync(string actionType, IRootState state)
    {
        if (!_enabled || IsActionTypeExcluded(actionType))
        {
            return;
        }

        try
        {
            await InvokeVoidAsync(
                JavaScriptMethods.SendToDevTools,
                new { type = actionType },
                state.GetStateDictionary())
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log but don't crash the application
            try
            {
                Console.WriteLine($"DevTools send failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    /// <summary>
    /// Subscribes to Redux DevTools messages (for time-travel support).
    /// </summary>
    private async Task SubscribeAsync()
    {
        if (!_enabled)
        {
            return;
        }

        try
        {
            DotNetObjectReference<ReduxDevToolsModule> dotNetRef = DotNetObjectReference.Create(this);
            await InvokeVoidAsync(JavaScriptMethods.SubscribeToDevTools, dotNetRef)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log but don't crash the application
            try
            {
                Console.WriteLine($"DevTools subscription failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    /// <summary>
    /// Invoked from JS when DevTools requests a state change (time-travel).
    /// </summary>
    /// <param name="jsonState">State as JSON (serialized from JS).</param>
    [JSInvokable]
    public Task OnDevToolsStateAsync(string jsonState)
    {
        try
        {
            if (!_options.EnableTimeTravel)
            {
                Console.WriteLine("DevTools time-travel is disabled");
                return Task.CompletedTask;
            }

            // Create a restore action from the JSON state
            DevToolsActions.RestoreState? restoreAction = _stateManager.CreateRestoreAction(jsonState);

            if (restoreAction is not null)
            {
                Console.WriteLine($"DevTools: Restoring state from time-travel");

                // Dispatch the restore action through the normal pipeline
                _dispatcher?.Dispatch(restoreAction);
            }
            else
            {
                Console.WriteLine("DevTools: Failed to parse state for time-travel");
            }
        }
        catch (Exception ex)
        {
            try
            {
                Console.WriteLine($"DevTools time-travel failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked from JS when DevTools requests a reset to initial state.
    /// </summary>
    [JSInvokable]
    public Task OnDevToolsResetAsync()
    {
        try
        {
            if (!_options.EnableTimeTravel)
            {
                Console.WriteLine("DevTools time-travel is disabled");
                return Task.CompletedTask;
            }

            Console.WriteLine("DevTools: Resetting to initial state");

            // Create and dispatch a reset action
            DevToolsActions.ResetToInitial resetAction = _stateManager.CreateResetAction();
            _dispatcher?.Dispatch(resetAction);
        }
        catch (Exception ex)
        {
            try
            {
                Console.WriteLine($"DevTools reset failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked from JS when DevTools requests jumping to a specific action.
    /// </summary>
    /// <param name="actionIndex">The action index to jump to.</param>
    /// <param name="actionType">The type of action being jumped to.</param>
    [JSInvokable]
    public async Task OnDevToolsJumpToActionAsync(int actionIndex, string actionType)
    {
        try
        {
            if (!_options.EnableTimeTravel)
            {
                Console.WriteLine("DevTools time-travel is disabled");
                return;
            }

            Console.WriteLine($"DevTools: Jumping to action {actionIndex} ({actionType})");

            // Invoke the callback if set
            if (_onJumpToState is not null)
            {
                await _onJumpToState(actionIndex).ConfigureAwait(false);
            }
            else
            {
                // Fallback: Create and dispatch a jump action
                DevToolsActions.JumpToAction jumpAction = new(actionIndex, actionType, DateTime.UtcNow);
                _dispatcher?.Dispatch(jumpAction);
            }
        }
        catch (Exception ex)
        {
            try
            {
                Console.WriteLine($"DevTools jump to action failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    /// <summary>
    /// Invoked from JS when DevTools requests committing the current state as the new baseline.
    /// </summary>
    [JSInvokable]
    public async Task OnDevToolsCommitAsync()
    {
        try
        {
            Console.WriteLine("DevTools: Commit current state as baseline");

            // Update the initial state to the current state
            if (_store is not null)
            {
                _stateManager.SetInitialState(_store.CurrentState().GetStateDictionary());
            }

            // Invoke the callback if set
            if (_onCommit is not null)
            {
                await _onCommit().ConfigureAwait(false);
            }
            else
            {
                // Fallback: dispatch a commit action for logging
                DevToolsActions.CommitState commitAction = new(DateTime.UtcNow);
                _dispatcher?.Dispatch(commitAction);
            }
        }
        catch (Exception ex)
        {
            try
            {
                Console.WriteLine($"DevTools commit failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    /// <summary>
    /// Invoked from JS when DevTools requests rolling back to the last committed state.
    /// </summary>
    [JSInvokable]
    public Task OnDevToolsRollbackAsync()
    {
        try
        {
            Console.WriteLine("DevTools: Rollback to committed state");

            // Create and dispatch a rollback action
            DevToolsActions.RollbackToCommitted rollbackAction = new(DateTime.UtcNow);
            _dispatcher?.Dispatch(rollbackAction);
        }
        catch (Exception ex)
        {
            try
            {
                Console.WriteLine($"DevTools rollback failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked from JS when DevTools requests sweeping (removing) skipped actions.
    /// </summary>
    [JSInvokable]
    public Task OnDevToolsSweepAsync()
    {
        try
        {
            Console.WriteLine("DevTools: Sweep skipped actions");

            // Create and dispatch a sweep action
            DevToolsActions.SweepSkippedActions sweepAction = new(DateTime.UtcNow);
            _dispatcher?.Dispatch(sweepAction);
        }
        catch (Exception ex)
        {
            try
            {
                Console.WriteLine($"DevTools sweep failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked from JS when DevTools requests toggling (skipping/unskipping) a specific action.
    /// </summary>
    /// <param name="actionIndex">The index of the action to toggle.</param>
    [JSInvokable]
    public Task OnDevToolsToggleActionAsync(int actionIndex)
    {
        try
        {
            Console.WriteLine($"DevTools: Toggle action at index {actionIndex}");

            // Create and dispatch a toggle action
            DevToolsActions.ToggleAction toggleAction = new(actionIndex, DateTime.UtcNow);
            _dispatcher?.Dispatch(toggleAction);
        }
        catch (Exception ex)
        {
            try
            {
                Console.WriteLine($"DevTools toggle action failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked from JS when DevTools requests importing a state.
    /// </summary>
    /// <param name="jsonState">The state to import as JSON.</param>
    [JSInvokable]
    public Task OnDevToolsImportStateAsync(string jsonState)
    {
        try
        {
            Console.WriteLine("DevTools: Import state");

            // Create a restore action from the imported JSON state
            DevToolsActions.RestoreState? importAction = _stateManager.CreateRestoreAction(jsonState);

            if (importAction is not null)
            {
                Console.WriteLine("DevTools: Importing state from external source");

                // Dispatch the import action through the normal pipeline
                _dispatcher?.Dispatch(importAction);
            }
            else
            {
                Console.WriteLine("DevTools: Failed to parse imported state");
            }
        }
        catch (Exception ex)
        {
            try
            {
                Console.WriteLine($"DevTools import state failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invoked from JS when DevTools requests recording control changes.
    /// </summary>
    /// <param name="isPaused">Whether recording is paused.</param>
    /// <param name="isLocked">Whether changes are locked.</param>
    [JSInvokable]
    public Task OnDevToolsRecordingControlAsync(bool isPaused, bool isLocked)
    {
        try
        {
            string actionType = isPaused ? "pause recording" : isLocked ? "lock changes" : "resume recording";
            Console.WriteLine($"DevTools: {actionType}");

            // Create and dispatch a recording control action
            DevToolsActions.RecordingControl recordingAction = new(isPaused, isLocked, DateTime.UtcNow);
            _dispatcher?.Dispatch(recordingAction);
        }
        catch (Exception ex)
        {
            try
            {
                Console.WriteLine($"DevTools recording control failed: {ex.Message}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates an enhanced action object with metadata for DevTools.
    /// </summary>
    /// <param name="action">The original action.</param>
    /// <param name="stackTrace">Optional stack trace for debugging.</param>
    /// <returns>Enhanced action object.</returns>
    private object CreateActionObject(object action, string? stackTrace = null)
    {
        Type actionType = action.GetType();

        return new
        {
            type = actionType.Name,
            payload = action,
            meta = new
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                source = "Ducky",
                trace = stackTrace ?? (_options.Trace ? Environment.StackTrace : null)
            }
        };
    }

    /// <summary>
    /// Determines if an action should be logged based on configuration.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <returns>True if the action should be logged; otherwise, false.</returns>
    private bool ShouldLogAction(object action)
    {
        // Never log DevTools internal actions
        if (action is IDevToolsAction)
        {
            return false;
        }

        // Check custom predicate first
        if (_options.ShouldLogAction is not null)
        {
            return _options.ShouldLogAction(action);
        }

        // Check excluded action types
        string actionType = action.GetType().Name;
        return !IsActionTypeExcluded(actionType);
    }

    /// <summary>
    /// Checks if an action type is in the excluded list.
    /// </summary>
    /// <param name="actionType">The action type to check.</param>
    /// <returns>True if the action type is excluded; otherwise, false.</returns>
    private bool IsActionTypeExcluded(string actionType)
    {
        return _options.ExcludedActionTypes.Contains(actionType, StringComparer.OrdinalIgnoreCase);
    }

    private static class JavaScriptMethods
    {
        public const string InitDevTools = "initDevTools";
        public const string SendToDevTools = "sendToDevTools";
        public const string SubscribeToDevTools = "subscribeToDevTools";
    }
}
