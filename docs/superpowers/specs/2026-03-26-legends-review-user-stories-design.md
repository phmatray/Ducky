# Legends Review: User Stories & Implementation Design

**Date:** 2026-03-26
**Source:** Legends Review consensus report (Elon 5.5/10, Steve 6/10, Linus 6/10)
**Approach:** Layered bottom-up — correctness, then generators, then features, then polish

---

## User Stories Summary

| ID | Title | Type | Layer |
|----|-------|------|-------|
| US-01 | Unify dispatch re-entrancy into single queue | bug | 1 - Core |
| US-02 | Thread-safe for Blazor Server + WASM | enhancement | 1 - Core |
| US-03 | ReactiveEffectBase IAsyncDisposable | bug | 1 - Core |
| US-04 | StateSnapshot O(1) lookups | enhancement | 1 - Core |
| US-05 | MemoizedSelector zero-alloc cache hit | bug | 1 - Core |
| US-06 | ObservableSlices thread safety | bug | 1 - Core |
| US-07 | Async effect error surfacing | bug | 1 - Core |
| US-08 | IAction marker + typed Dispatch<T> | enhancement | 2 - Generators |
| US-09 | [DuckyReducer] source generator | enhancement | 2 - Generators |
| US-10 | [DuckyEffect] source generator | enhancement | 2 - Generators |
| US-11 | Finish CrossTabSync hydration | bug | 3 - Features |
| US-12 | Finish DevTools time-travel | enhancement | 3 - Features |
| US-13 | Two DI entry points only | enhancement | 4 - Polish |
| US-14 | Actionable error messages | enhancement | 4 - Polish |
| US-15 | DuckyComponent catch-all in async void | bug | 4 - Polish |
| US-16 | PersistenceInitializer missing warning | enhancement | 4 - Polish |
| US-17 | Eliminate two-phase initialization | enhancement | 4 - Polish |

---

## Layer 1: Core Foundation (Correctness)

### US-01: Unify Dispatch Re-entrancy

**Problem:** `Dispatcher.DequeueActions()` has `_isDequeuing` guard + queue. `DuckyStore.ProcessActionSafely()` has `_isDispatching` guard + `_reentrantQueue`. Two independent state machines with undefined ordering.

**Design:**
- Remove re-entrancy handling from `Dispatcher` entirely
- `Dispatcher` becomes a thin event emitter: `Dispatch()` fires `ActionDispatched`, nothing more
- `DuckyStore` owns the single re-entrant queue with documented FIFO guarantee
- Re-entrant actions appended and processed after current action completes
- Keep max depth 10 but log warning via `ILogger` when actions are queued
- Throw `InvalidOperationException` when depth exceeded instead of silent abort

**Files changed:**
- `src/library/Ducky/Dispatcher.cs` — remove `_isDequeuing`, simplify to event emitter
- `src/library/Ducky/DuckyStore.cs` — sole owner of re-entrant queue
- `src/tests/Ducky.Tests/Core/DispatcherTests.cs` — update tests
- `src/tests/Ducky.Tests/Core/DuckyStoreReentrancyTests.cs` — add ordering guarantee tests

### US-02: Thread-Safety Contract

**Problem:** Mixed synchronization: some classes use locks, some volatile, some neither. No documented contract.

**Design:**
- All mutable shared state protected by `lock` (not volatile flags)
- `DuckyStore`: single `lock(_syncRoot)` around `ProcessActionSafely` and state access
- `ObservableSlices`: `ReaderWriterLockSlim` for concurrent reads, exclusive writes
- `Dispatcher`: `lock` on dispatch queue (already exists, remove volatile `_isDequeuing`)
- `SliceReducers`: `lock` around lazy `_isInitialized` / `_currentState`
- Document in XML docs: "This library is thread-safe. All public members can be called from any thread."

**Files changed:**
- `src/library/Ducky/DuckyStore.cs` — replace volatile with lock
- `src/library/Ducky/ObservableSlices.cs` — add ReaderWriterLockSlim
- `src/library/Ducky/Dispatcher.cs` — remove volatile flags
- `src/library/Ducky/SliceReducers.cs` — add lock on lazy init
- New: `src/tests/Ducky.Tests/Core/ThreadSafetyTests.cs` — concurrent dispatch tests

### US-03: ReactiveEffectBase IAsyncDisposable

**Problem:** `ReactiveEffectBase.Dispose()` calls `OnDisposeAsync().GetAwaiter().GetResult()` — deadlock risk in sync contexts.

**Design:**
- Implement `IAsyncDisposable` on `ReactiveEffectBase`
- `DisposeAsync()` calls `OnDisposeAsync()` properly
- `IDisposable.Dispose()` becomes no-op fallback that logs warning if async cleanup skipped
- DI container calls `DisposeAsync()` automatically

**Files changed:**
- `src/library/Ducky/Reactive/Base/ReactiveEffectBase.cs`
- `src/tests/Ducky.Tests/Reactive/ReactiveEffectBaseTests.cs`

### US-04: StateSnapshot O(1) Lookups

**Problem:** `StateSnapshot.GetSlice<T>()` does O(n) linear scan. `GetStateDictionary()` rebuilds `ImmutableSortedDictionary` on every action (O(n log n)).

**Design:**
- Add `Dictionary<Type, string>` type-to-key index built once at StateSnapshot construction
- `GetSlice<T>()` does O(1) lookup by type, then O(1) by key
- Cache `ImmutableSortedDictionary` in `ObservableSlices` — invalidate only when slice state changes

**Files changed:**
- `src/library/Ducky/Pipeline/StateSnapshot.cs`
- `src/library/Ducky/ObservableSlices.cs` — cache GetStateDictionary()
- `src/tests/Ducky.Tests/Core/StateSnapshotTests.cs`

### US-05: MemoizedSelector Zero-Alloc Cache Hit

**Problem:** `Array.ConvertAll(dependencies, dep => dep(state))` runs before cache check — allocates on every call including cache hits.

**Design:**
- Check `ReferenceEquals(state, lastState)` first
- If same reference, return `lastResult` immediately — zero allocation
- Only allocate dependency array when state reference changes

**Files changed:**
- `src/library/Ducky/Selectors/MemoizedSelector.cs`
- `src/tests/Ducky.Tests/Core/MemoizedSelectorTests.cs`

### US-06: ObservableSlices Thread Safety

Covered by US-02. `ReaderWriterLockSlim` on `_slices`, `_slicesByStateType`, `_sliceUpdateHandlers`.

### US-07: Async Effect Error Surfacing

**Problem:** `Task.Run` in `AsyncEffectMiddleware` publishes errors via events. If nobody subscribes, errors vanish.

**Design:**
- Add `ILogger.LogError()` as primary error output (always available)
- Keep event publishing as secondary channel
- Add `EffectOptions.ThrowOnEffectError` (default `false`) for strict mode in development

**Files changed:**
- `src/library/Ducky/Middlewares/AsyncEffect/AsyncEffectMiddleware.cs`
- New: `src/library/Ducky/Middlewares/AsyncEffect/EffectOptions.cs`
- `src/tests/Ducky.Tests/Middlewares/AsyncEffectMiddlewareTests.cs`

---

## Layer 2: Source Generators (API Reshape)

### US-08: IAction Marker Interface + Typed Dispatch

**Problem:** `Dispatch(object action)` abandons C# type system at the most important boundary.

**Design:**
- Add `IAction` marker interface to Ducky core (empty, no members)
- Add `Dispatch<TAction>(TAction action) where TAction : IAction` to `IDispatcher`
- Mark `Dispatch(object)` as `[Obsolete]` for backward compat
- `[DuckyAction]` source generator auto-adds `: IAction` on attributed partial records
- Actions without `[DuckyAction]` can manually implement `IAction`
- Middleware pipeline internally still passes `object` (no breaking change to IMiddleware)

**Files changed:**
- New: `src/library/Ducky/Abstractions/IAction.cs`
- `src/library/Ducky/Abstractions/IDispatcher.cs` — add generic overload
- `src/library/Ducky/Dispatcher.cs` — implement generic overload
- `src/library/Ducky.Generator/ActionDispatcherSourceGenerator.cs` — add `: IAction`
- New: `src/library/Ducky.Generator/Sources/IActionSource.cs` — embedded source

### US-09: [DuckyReducer] Source Generator

**Problem:** Users must create `SliceReducers<TState>` subclasses and register each reducer with `On<TAction>()` manually.

**Design:**
```csharp
// User writes:
[DuckyReducer]
public static partial class CounterReducers
{
    public static CounterState On(CounterState state, Increment action)
        => state with { Count = state.Count + action.Amount };

    public static CounterState On(CounterState state, Reset _)
        => new CounterState();
}

// Generator produces CounterReducersSlice : SliceReducers<CounterState>
```

**Rules:**
- All `On` methods must share the same first parameter type (the state)
- Second parameter must implement `IAction`
- `[InitialState]` attribute on static field/property for initial state, or `new TState()` default
- Generated class name: `{ClassName}Slice`
- Registered via `AddSlice<CounterReducersSlice>()`

**Files changed:**
- New: `src/library/Ducky.Generator/ReducerSourceGenerator.cs`
- New: `src/library/Ducky/Attributes/DuckyReducerAttribute.cs`
- New: `src/library/Ducky/Attributes/InitialStateAttribute.cs`
- New: `src/library/Ducky.Generator/Sources/DuckyReducerAttributeSource.cs`
- New: `src/tests/Ducky.Tests/Generator/ReducerSourceGeneratorTests.cs`

### US-10: [DuckyEffect] Source Generator

**Problem:** Each async effect needs a separate class with constructor injection, CanHandle, HandleAsync.

**Design:**
```csharp
// User writes:
[DuckyEffect]
public static partial class CounterEffects
{
    public static async Task OnIncrement(
        Increment action,
        IStateProvider state,
        IDispatcher dispatcher,
        CancellationToken ct)
    {
        if (state.GetSlice<CounterState>().Count > 100)
            dispatcher.Dispatch(new MilestoneReached(100));
    }
}

// Generator produces one AsyncEffect<TAction> class per method
```

**Rules:**
- First parameter: `IAction` type (trigger action)
- Framework params (auto-provided): `IStateProvider`, `IDispatcher`, `CancellationToken`
- All other params: resolved from DI at construction time
- Must return `Task`
- Reactive effects still require manual classes (too complex for generation)
- Generated class name: `{ClassName}_{MethodName}_Effect`
- Auto-discovered by assembly scanning

**Files changed:**
- New: `src/library/Ducky.Generator/EffectSourceGenerator.cs`
- New: `src/library/Ducky/Attributes/DuckyEffectAttribute.cs`
- New: `src/library/Ducky.Generator/Sources/DuckyEffectAttributeSource.cs`
- New: `src/tests/Ducky.Tests/Generator/EffectSourceGeneratorTests.cs`

---

## Layer 3: Finish Features

### US-11: CrossTabSync Hydration

**Problem:** `OnExternalStateChangedAsync()` is a no-op returning `Task.CompletedTask`.

**Design:**

**Flow:**
1. Tab A: action → state change → PersistenceMiddleware saves to localStorage
2. Browser fires `storage` event in Tab B
3. JS calls `OnExternalStateChangedAsync()` in Tab B
4. Callback reads new state from localStorage via `IPersistenceProvider.LoadAsync()`
5. Dispatches `HydrateSliceAction` for each changed slice
6. Debounce (default 100ms) prevents rapid-fire hydration

**Loop prevention:** PersistenceMiddleware compares state hash after hydration — if hash matches what was just loaded, skip persistence. Existing SHA256 check handles this.

**JS changes:** `addReduxStorageListener` passes storage event key to .NET for filtering.

**Options:**
```csharp
public class CrossTabSyncOptions
{
    public bool Enabled { get; set; } = true;
    public int DebounceMs { get; set; } = 100;
    public string[]? IncludedSliceKeys { get; set; } // null = all
}
```

**Files changed:**
- `src/library/Ducky.Blazor/CrossTabSync/CrossTabSyncModule.cs` — implement callback
- `src/library/Ducky.Blazor/CrossTabSync/CrossTabSync.razor.cs` — pass options
- New: `src/library/Ducky.Blazor/CrossTabSync/CrossTabSyncOptions.cs`
- `src/library/Ducky.Blazor/wwwroot/*.js` — pass storage key in callback
- `src/library/Ducky.Blazor/DuckyBlazorServiceCollectionExtensions.cs` — register options
- New: `src/tests/Ducky.Blazor.Tests/CrossTabSync/CrossTabSyncTests.cs`

### US-12: DevTools Time-Travel

**Problem:** Six handlers in `DevToolsReducer` are stubs with TODO comments.

**Design:**

**State history:** `DevToolsMiddleware` maintains `List<DevToolsStateEntry>` capped at `MaxAge`:
```csharp
internal record DevToolsStateEntry(
    int SequenceNumber,
    object Action,
    string SerializedState,
    bool IsSkipped,
    DateTime Timestamp);
```

**State restoration:** Add `IStore.RestoreState(IReadOnlyDictionary<string, object> stateDict)` — bypasses pipeline, sets each slice directly, fires `StateChanged`.

**Handler implementations:**

| Handler | Behavior |
|---------|----------|
| JumpToAction(index) | Restore snapshot at index. Block user actions until resume. |
| ReplayActions(start, end) | Restore at start, re-dispatch each action start+1..end. |
| RollbackToCommitted | Restore last committed snapshot. Clear history after. |
| SweepSkippedActions | Remove skipped entries. Replay non-skipped from initial. |
| ToggleAction(index) | Flip IsSkipped. Replay all non-skipped from initial. |
| RecordingControl | Set `_isRecording` flag. Paused = skip DevTools notification. |

**Guard:** During time-travel (current != latest), `MayDispatchAction()` blocks user actions but allows `RestoreState` and `DevToolsAction` types.

**Files changed:**
- `src/library/Ducky.Blazor/Middlewares/DevTools/DevToolsMiddleware.cs` — history tracking
- `src/library/Ducky.Blazor/Middlewares/DevTools/DevToolsReducer.cs` — implement handlers
- `src/library/Ducky.Blazor/Middlewares/DevTools/DevToolsStateManager.cs` — restoration
- `src/library/Ducky.Blazor/Middlewares/DevTools/ReduxDevToolsModule.cs` — wire callbacks
- New: `src/library/Ducky.Blazor/Middlewares/DevTools/DevToolsStateEntry.cs`
- `src/library/Ducky/Abstractions/IStore.cs` — add RestoreState method
- `src/library/Ducky/DuckyStore.cs` — implement RestoreState
- New: `src/tests/Ducky.Blazor.Tests/Middlewares/DevTools/TimeTravelTests.cs`

---

## Layer 4: DX Polish

### US-13: Two DI Entry Points

**Design:**
- Keep `AddDucky(Action<DuckyBuilder>)` in core
- Keep `AddDuckyBlazor(Action<BlazorDuckyBuilder>)` in Blazor
- Delete: `AddDuckyStore`, `AddDuckyWithMiddleware`, `AddDuckyWithPipeline`, `AddDuckyStoreWithReactiveEffects`
- Clean break, no `[Obsolete]`. Document migration in CHANGELOG.

**Files changed:**
- `src/library/Ducky/DuckyServiceCollectionExtensions.cs` — remove extra methods
- `src/library/Ducky.Blazor/DuckyBlazorServiceCollectionExtensions.cs` — remove extras
- Update all demos and tests that reference removed methods

### US-14: Actionable Error Messages

**Design:** Create `ExceptionFactory` internal static class. All throws go through it.

| Current | Replacement |
|---------|-------------|
| `"State is null."` | `"State for slice '{key}' is null. Ensure the reducer returns non-null initial state from GetInitialState()."` |
| `"The entity does not exist."` | `"Entity with key '{key}' not found in NormalizedState<{TKey}, {TEntity}>. Use HasEntity() or TryGetEntity()."` |
| `"Action was aborted."` | `"Action '{type}' aborted: re-entrant depth exceeded {max}. Check '{last}' → '{type}' dispatch chain."` |
| bare ObjectDisposedException | `"Cannot dispatch to disposed DuckyStore. Ensure components unsubscribe in Dispose()."` |

**Files changed:**
- New: `src/library/Ducky/ExceptionFactory.cs`
- All files that throw DuckyException — update to use factory
- `src/library/Ducky/Normalization/NormalizedState.cs` — context in exceptions

### US-15: DuckyComponent Catch-All

**Design:** Replace `catch (ObjectDisposedException)` with `catch (Exception ex)` in both `DuckyComponent.OnStateChanged` and `DuckySelectorComponent.OnStateChanged`. Log via `ILogger.LogError`. Don't rethrow.

**Files changed:**
- `src/library/Ducky.Blazor/Components/DuckyComponent.cs`
- `src/library/Ducky.Blazor/Components/DuckySelectorComponent.cs`

### US-16: PersistenceInitializer Missing Warning

**Design:** In `PersistenceMiddleware.InitializeAsync()`, start 5-second timer. If `HydrateAsync()` not called by then, `ILogger.LogWarning("Persistence configured but HydrateAsync() not called. Add <PersistenceInitializer> to layout.")`. Configurable via `PersistenceOptions.HydrationTimeoutWarningMs`.

**Files changed:**
- `src/library/Ducky.Blazor/Middlewares/Persistence/PersistenceMiddleware.cs`
- `src/library/Ducky.Blazor/Middlewares/Persistence/PersistenceOptions.cs`

### US-17: Eliminate Two-Phase Initialization

**Design:** Move `InitializeAsync()` into DI factory for sync parts. Async-only features (DevTools JS, persistence hydration) initialize via `IHostedService` or lazily on first use. Mark `InitializeAsync()` as `[Obsolete]`.

**Files changed:**
- `src/library/Ducky/Builder/DuckyBuilder.cs` — factory registration
- `src/library/Ducky/DuckyStore.cs` — split sync/async init
- New: `src/library/Ducky.Blazor/Services/DuckyStoreAsyncInitializer.cs`
- `src/library/Ducky.Blazor/Services/DuckyStoreInitializer.cs` — update

---

## GitHub Issues

Each user story becomes one GitHub issue. Labels:
- US-01 through US-07: `bug` (Layer 1)
- US-08 through US-10: `enhancement` (Layer 2)
- US-11: `bug` (Layer 3)
- US-12: `enhancement` (Layer 3)
- US-13 through US-17: `enhancement` or `bug` as noted (Layer 4)

Issues should reference this spec and include the layer number for prioritization.

---

## Dependencies

```
US-01 (re-entrancy) ──┐
US-02 (thread-safe) ──┤
US-06 (observable)  ──┘──→ US-08 (IAction) ──→ US-09 (reducer gen) ──→ US-11 (CrossTabSync)
                                              └→ US-10 (effect gen)  └→ US-12 (DevTools)
US-03 (dispose) ─────────────────────────────────────────────────────→ US-13 (DI cleanup)
US-04 (snapshot) ────────────────────────────────────────────────────→ US-14 (errors)
US-05 (selector) ────────────────────────────────────────────────────→ US-15 (catch-all)
US-07 (effect errors) ──────────────────────────────────────────────→ US-16 (persistence warn)
                                                                     → US-17 (init cleanup)
```

Layer 1 items are independent of each other and can be parallelized.
Layer 2 depends on US-08 (IAction) being done first, then US-09/US-10 can parallelize.
Layer 3 depends on Layer 2 (new generators used in implementations).
Layer 4 items are independent and can parallelize after Layer 3.
