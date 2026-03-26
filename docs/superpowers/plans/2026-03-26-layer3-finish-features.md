# Layer 3: Finish Features Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete two unfinished features — CrossTabSync hydration and DevTools time-travel state restoration.

**Architecture:** US-11 (CrossTabSync) and US-12 (DevTools) are independent and can be parallelized. Both build on the corrected core from Layer 1.

**Tech Stack:** .NET 9, C# 13, Blazor WebAssembly, JavaScript Interop, xUnit, bUnit

**Spec:** `docs/superpowers/specs/2026-03-26-legends-review-user-stories-design.md`

**Issues:** #222, #223

**Prerequisite:** Layers 1 and 2 must be complete.

---

### Task 1: CrossTabSync Hydration (US-11)

**Files:**
- Create: `src/library/Ducky.Blazor/CrossTabSync/CrossTabSyncOptions.cs`
- Modify: `src/library/Ducky.Blazor/CrossTabSync/CrossTabSyncModule.cs`
- Modify: `src/library/Ducky.Blazor/CrossTabSync/CrossTabSync.razor.cs`
- Modify: `src/library/Ducky.Blazor/DuckyBlazorServiceCollectionExtensions.cs`
- Create: `src/tests/Ducky.Blazor.Tests/CrossTabSync/CrossTabSyncModuleTests.cs`

- [ ] **Step 1: Create CrossTabSyncOptions**

Create `src/library/Ducky.Blazor/CrossTabSync/CrossTabSyncOptions.cs`:

```csharp
// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Blazor.CrossTabSync;

/// <summary>
/// Configuration options for cross-tab state synchronization.
/// </summary>
public class CrossTabSyncOptions
{
    /// <summary>
    /// Whether cross-tab sync is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Debounce delay in milliseconds to prevent rapid-fire hydration. Default: 100.
    /// </summary>
    public int DebounceMs { get; set; } = 100;

    /// <summary>
    /// If specified, only these slice keys are synchronized across tabs.
    /// Null means all slices are synchronized.
    /// </summary>
    public string[]? IncludedSliceKeys { get; set; }
}
```

- [ ] **Step 2: Implement OnExternalStateChangedAsync**

Read the current `CrossTabSyncModule.cs` and replace the no-op callback. The module needs access to `IPersistenceProvider` (or `IPersistenceService`) and `IDispatcher`:

Update `CrossTabSyncModule.cs` to inject dependencies and implement the callback:

```csharp
// Key changes to CrossTabSyncModule:
// 1. Inject IPersistenceProvider and IDispatcher
// 2. Add debounce timer
// 3. Implement OnExternalStateChangedAsync

private readonly IPersistenceProvider _persistenceProvider;
private readonly IDispatcher _dispatcher;
private readonly CrossTabSyncOptions _options;
private readonly ILogger<CrossTabSyncModule> _logger;
private Timer? _debounceTimer;

[JSInvokable]
public Task OnExternalStateChangedAsync(string? storageKey)
{
    if (!_options.Enabled)
    {
        return Task.CompletedTask;
    }

    // Debounce: cancel previous timer and start new one
    _debounceTimer?.Dispose();
    _debounceTimer = new Timer(
        async _ => await HydrateFromStorageAsync(),
        null,
        _options.DebounceMs,
        Timeout.Infinite);

    return Task.CompletedTask;
}

private async Task HydrateFromStorageAsync()
{
    try
    {
        var persistedState = await _persistenceProvider.LoadAsync();
        if (persistedState is null)
        {
            return;
        }

        foreach (var (sliceKey, state) in persistedState.Slices)
        {
            // Filter by included slice keys if specified
            if (_options.IncludedSliceKeys is not null
                && !_options.IncludedSliceKeys.Contains(sliceKey))
            {
                continue;
            }

            _dispatcher.Dispatch(new HydrateSliceAction(sliceKey, state));
        }

        _logger.LogDebug("Cross-tab sync: hydrated {Count} slices from external tab.",
            persistedState.Slices.Count);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Cross-tab sync: failed to hydrate from external tab.");
    }
}
```

- [ ] **Step 3: Update JS to pass storage key**

Find the JS file in `src/library/Ducky.Blazor/wwwroot/` that contains `addReduxStorageListener` and update it to pass the storage event key to .NET:

```javascript
// Update the storage event listener to pass the key
window.addEventListener('storage', function(event) {
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnExternalStateChangedAsync', event.key);
    }
});
```

- [ ] **Step 4: Register CrossTabSyncOptions in DI**

In `DuckyBlazorServiceCollectionExtensions.cs`, update `EnableCrossTabSync`:

```csharp
public BlazorDuckyBuilder EnableCrossTabSync(Action<CrossTabSyncOptions>? configure = null)
{
    var options = new CrossTabSyncOptions();
    configure?.Invoke(options);
    _services.AddSingleton(options);
    _services.AddScoped<CrossTabSyncModule>();
    _crossTabSyncEnabled = true;
    return this;
}
```

- [ ] **Step 5: Write tests**

Create `src/tests/Ducky.Blazor.Tests/CrossTabSync/CrossTabSyncModuleTests.cs` with unit tests for the hydration logic using faked dependencies.

- [ ] **Step 6: Run tests**

Run: `dotnet test src/tests/Ducky.Blazor.Tests --filter "FullyQualifiedName~CrossTabSync" -v normal`
Expected: All pass.

- [ ] **Step 7: Commit**

```bash
git add src/library/Ducky.Blazor/CrossTabSync/ src/library/Ducky.Blazor/DuckyBlazorServiceCollectionExtensions.cs src/tests/Ducky.Blazor.Tests/
git commit -m "feat: implement CrossTabSync hydration callback with debounce (#222)"
```

---

### Task 2: DevTools Time-Travel (US-12)

**Files:**
- Create: `src/library/Ducky.Blazor/Middlewares/DevTools/DevToolsStateEntry.cs`
- Modify: `src/library/Ducky.Blazor/Middlewares/DevTools/DevToolsMiddleware.cs`
- Modify: `src/library/Ducky.Blazor/Middlewares/DevTools/DevToolsReducer.cs`
- Modify: `src/library/Ducky.Blazor/Middlewares/DevTools/DevToolsStateManager.cs`
- Modify: `src/library/Ducky.Blazor/Middlewares/DevTools/ReduxDevToolsModule.cs`
- Modify: `src/library/Ducky/Abstractions/IStore.cs`
- Modify: `src/library/Ducky/DuckyStore.cs`
- Create: `src/tests/Ducky.Blazor.Tests/Middlewares/DevTools/TimeTravelTests.cs`

- [ ] **Step 1: Create DevToolsStateEntry**

Create `src/library/Ducky.Blazor/Middlewares/DevTools/DevToolsStateEntry.cs`:

```csharp
// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Represents a single entry in the DevTools action history for time-travel debugging.
/// </summary>
internal record DevToolsStateEntry(
    int SequenceNumber,
    object Action,
    string SerializedState,
    bool IsSkipped,
    DateTime Timestamp)
{
    /// <summary>
    /// Creates a copy with the IsSkipped flag toggled.
    /// </summary>
    public DevToolsStateEntry WithToggledSkip()
        => this with { IsSkipped = !IsSkipped };
}
```

- [ ] **Step 2: Add RestoreState to IStore and DuckyStore**

Add to `IStore.cs`:

```csharp
    /// <summary>
    /// Restores state from a dictionary, bypassing the dispatch pipeline.
    /// Used by DevTools time-travel debugging.
    /// </summary>
    /// <param name="stateDict">Dictionary of slice key to state object.</param>
    void RestoreState(IReadOnlyDictionary<string, object> stateDict);
```

Implement in `DuckyStore.cs`:

```csharp
    /// <inheritdoc/>
    public void RestoreState(IReadOnlyDictionary<string, object> stateDict)
    {
        ArgumentNullException.ThrowIfNull(stateDict);

        foreach (var (key, state) in stateDict)
        {
            _dispatcher.Dispatch(new HydrateSliceAction(key, state));
        }
    }
```

- [ ] **Step 3: Add history tracking to DevToolsMiddleware**

Add fields to `DevToolsMiddleware`:

```csharp
    private readonly List<DevToolsStateEntry> _history = [];
    private int _committedIndex = -1;
    private bool _isRecording = true;
```

In `AfterReduce`, after sending to DevTools, also record history:

```csharp
    if (_isRecording)
    {
        var serializedState = _stateManager.SerializeState(_store);
        var entry = new DevToolsStateEntry(
            _sequenceNumberOfLatestState,
            action,
            serializedState,
            IsSkipped: false,
            DateTime.UtcNow);

        _history.Add(entry);

        // Enforce MaxAge limit
        while (_history.Count > _options.MaxAge)
        {
            _history.RemoveAt(0);
        }
    }
```

- [ ] **Step 4: Implement time-travel handlers in DevToolsReducer**

Replace the TODO stubs with real implementations. Each handler calls back into `DevToolsMiddleware` methods:

```csharp
// JumpToAction — restore state at given index
public void HandleJumpToAction(int actionIndex)
{
    if (actionIndex < 0 || actionIndex >= _history.Count)
        return;

    var entry = _history[actionIndex];
    var stateDict = _stateManager.DeserializeState(entry.SerializedState);
    _store.RestoreState(stateDict);
    _sequenceNumberOfCurrentState = entry.SequenceNumber;
}

// RollbackToCommitted — restore to last commit point
public void HandleRollbackToCommitted()
{
    if (_committedIndex < 0 || _committedIndex >= _history.Count)
        return;

    var entry = _history[_committedIndex];
    var stateDict = _stateManager.DeserializeState(entry.SerializedState);
    _store.RestoreState(stateDict);
    _history.RemoveRange(_committedIndex + 1, _history.Count - _committedIndex - 1);
    _sequenceNumberOfCurrentState = entry.SequenceNumber;
    _sequenceNumberOfLatestState = entry.SequenceNumber;
}

// SweepSkippedActions — remove skipped, replay non-skipped
public void HandleSweepSkippedActions()
{
    _history.RemoveAll(e => e.IsSkipped);
    ReplayFromInitialState();
}

// ToggleAction — flip skip flag, replay
public void HandleToggleAction(int actionIndex)
{
    if (actionIndex < 0 || actionIndex >= _history.Count)
        return;

    _history[actionIndex] = _history[actionIndex].WithToggledSkip();
    ReplayFromInitialState();
}

// RecordingControl — pause/resume recording
public void HandleRecordingControl(bool isPaused)
{
    _isRecording = !isPaused;
}

// CommitState — set current as committed baseline
public void HandleCommit()
{
    _committedIndex = _history.Count - 1;
}

private void ReplayFromInitialState()
{
    // Restore initial state
    var initialState = _stateManager.GetInitialState();
    _store.RestoreState(initialState);

    // Replay non-skipped actions
    foreach (var entry in _history.Where(e => !e.IsSkipped))
    {
        _dispatcher.Dispatch(entry.Action);
    }
}
```

- [ ] **Step 5: Update MayDispatchAction for time-travel guard**

In `DevToolsMiddleware.MayDispatchAction`:

```csharp
    public override bool MayDispatchAction(object action)
    {
        // During time-travel, block user actions but allow internal ones
        if (_sequenceNumberOfCurrentState != _sequenceNumberOfLatestState)
        {
            return action is HydrateSliceAction
                || action.GetType().Namespace?.Contains("DevTools") == true;
        }

        return true;
    }
```

- [ ] **Step 6: Wire ReduxDevToolsModule callbacks to handler methods**

Update the JSInvokable callbacks in `ReduxDevToolsModule.cs` to call the middleware handler methods instead of logging.

- [ ] **Step 7: Write time-travel tests**

Create `src/tests/Ducky.Blazor.Tests/Middlewares/DevTools/TimeTravelTests.cs`:

```csharp
[Fact]
public void JumpToAction_ShouldRestoreStateAtIndex()
{
    // Arrange - set up store with DevTools, dispatch 3 actions
    // Act - jump to action index 1
    // Assert - state matches what it was after action 1
}

[Fact]
public void ToggleAction_ShouldReplayWithoutSkippedAction()
{
    // Arrange - dispatch Increment(1), Increment(2), Increment(3)
    // Act - toggle action index 1 (skip Increment(2))
    // Assert - state = 1 + 3 = 4, not 1 + 2 + 3 = 6
}

[Fact]
public void SweepSkippedActions_ShouldRemoveSkippedFromHistory()
{
    // Arrange - dispatch 3 actions, skip middle one
    // Act - sweep
    // Assert - history has 2 entries
}
```

- [ ] **Step 8: Run tests**

Run: `dotnet test src/tests/Ducky.Blazor.Tests --filter "FullyQualifiedName~TimeTravel" -v normal`
Expected: All pass.

- [ ] **Step 9: Commit**

```bash
git add src/library/Ducky.Blazor/Middlewares/DevTools/ src/library/Ducky/Abstractions/IStore.cs src/library/Ducky/DuckyStore.cs src/tests/
git commit -m "feat: implement DevTools time-travel with state history and restoration (#223)"
```
