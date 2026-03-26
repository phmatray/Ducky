# Layer 1: Core Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all correctness bugs in the core library — unified re-entrancy, thread safety, proper disposal, O(1) performance, and reliable error surfacing.

**Architecture:** Six independent fixes to the core Ducky library. US-01 (re-entrancy) and US-02 (thread safety) overlap on Dispatcher and DuckyStore, so they are combined into a single task. US-06 (ObservableSlices) is covered by US-02.

**Tech Stack:** .NET 9, C# 13, xUnit, Shouldly, FakeItEasy

**Spec:** `docs/superpowers/specs/2026-03-26-legends-review-user-stories-design.md`

**Issues:** #213, #214, #215, #216, #217, #218

---

### Task 1: Unify Dispatch Re-entrancy + Thread Safety (US-01, US-02, US-06)

**Files:**
- Modify: `src/library/Ducky/Dispatcher.cs`
- Modify: `src/library/Ducky/DuckyStore.cs`
- Modify: `src/library/Ducky/ObservableSlices.cs`
- Modify: `src/library/Ducky/SliceReducers.cs`
- Modify: `src/tests/Ducky.Tests/Core/DispatcherTests.cs`
- Modify: `src/tests/Ducky.Tests/Core/DuckyStoreReentrancyTests.cs`
- Create: `src/tests/Ducky.Tests/Core/ThreadSafetyTests.cs`

- [ ] **Step 1: Simplify Dispatcher to thin event emitter**

Remove re-entrancy handling from `Dispatcher`. It becomes a pass-through that fires events.

Replace the entire `Dispatcher.cs` with:

```csharp
// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// A dispatcher that dispatches actions and provides events for dispatched actions.
/// This class is thread-safe. All public members can be called from any thread.
/// </summary>
public sealed class Dispatcher : IDispatcher, IDisposable
{
    private readonly object _syncRoot = new();
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

    /// <inheritdoc />
    public object? LastAction { get; private set; }

    /// <inheritdoc />
    public void Dispatch(object action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_syncRoot)
        {
            if (_disposed)
            {
                throw new DuckyException(
                    "Cannot dispatch to a disposed Dispatcher. Ensure components unsubscribe in Dispose().",
                    new ObjectDisposedException(nameof(Dispatcher)));
            }

            LastAction = action;
        }

        // Fire event outside lock to avoid deadlocks with re-entrant dispatch
        ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
    }

    /// <summary>
    /// Releases all resources used by the <see cref="Dispatcher"/> class.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ActionDispatched = null;
        }
    }
}
```

- [ ] **Step 2: Update DuckyStore to be sole re-entrancy owner with proper locking**

Replace volatile flags with lock-based synchronization in `DuckyStore.cs`. Key changes:
- Remove `volatile` from `_isDisposed` and `_isDispatching`
- Add `ILogger` parameter for re-entrant warnings
- Throw `InvalidOperationException` when max depth exceeded
- Check `_isDisposed` at entry to `ProcessActionSafely`

In `DuckyStore.cs`, update the fields (lines 25-27):

```csharp
    private readonly ILogger<DuckyStore> _logger;
    private bool _isDisposed;
    private bool _isDispatching;
```

Update the constructor to accept `ILogger<DuckyStore>`:

```csharp
    public DuckyStore(
        IDispatcher dispatcher,
        ActionPipeline pipeline,
        IStoreEventPublisher eventPublisher,
        IEnumerable<ISlice> slices,
        ILogger<DuckyStore> logger)
```

Store it: `_logger = logger;`

Replace `ProcessActionSafely` (lines 137-197):

```csharp
    private void ProcessActionSafely(object action)
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                _logger.LogWarning("Action {ActionType} dispatched to disposed store, ignoring.",
                    action.GetType().Name);
                return;
            }

            if (_isDispatching)
            {
                if (_reentrantQueue.Count >= MaxReentrantDepth)
                {
                    var message = $"Action '{action.GetType().Name}' aborted: re-entrant dispatch depth exceeded maximum of {MaxReentrantDepth}. Check the dispatch chain from '{_currentAction?.GetType().Name}' to '{action.GetType().Name}'.";
                    _logger.LogError(message);
                    _eventPublisher.Publish(new ActionAbortedEventArgs(
                        new ActionContext(action) { StateProvider = _slices },
                        message));
                    throw new InvalidOperationException(message);
                }

                _logger.LogDebug("Re-entrant dispatch detected: {ActionType} queued (depth {Depth}).",
                    action.GetType().Name, _reentrantQueue.Count + 1);
                _eventPublisher.Publish(new ActionReentrantEventArgs(
                    action, _currentAction!, _reentrantQueue.Count + 1));
                _reentrantQueue.Enqueue(action);
                return;
            }

            _isDispatching = true;
        }

        try
        {
            _currentAction = action;
            ProcessAction(action);
        }
        finally
        {
            while (true)
            {
                object? nextAction;
                lock (_syncRoot)
                {
                    if (_reentrantQueue.Count == 0)
                    {
                        _isDispatching = false;
                        _currentAction = null;
                        break;
                    }

                    nextAction = _reentrantQueue.Dequeue();
                }

                try
                {
                    _currentAction = nextAction;
                    ProcessAction(nextAction);
                }
                catch (Exception ex)
                {
                    ActionContext context = new(nextAction) { StateProvider = _slices };
                    _eventPublisher.Publish(new ActionErrorEventArgs(ex, nextAction, context));
                }
            }
        }
    }
```

Update `Dispose` to use lock instead of volatile check:

```csharp
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        TimeSpan uptime = DateTime.UtcNow - _startTime;
        _eventPublisher.Publish(new StoreDisposingEventArgs(uptime));

        _dispatcher.ActionDispatched -= OnActionDispatched;
        _slices.SliceStateChanged -= OnSliceStateChanged;
        _pipeline.Dispose();
        _slices.Dispose();

        lock (_syncRoot)
        {
            _reentrantQueue.Clear();
        }
    }
```

- [ ] **Step 3: Add ReaderWriterLockSlim to ObservableSlices**

In `ObservableSlices.cs`, add the lock field:

```csharp
    private readonly ReaderWriterLockSlim _rwLock = new();
```

Wrap `AddSlice` in a write lock:

```csharp
    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);

        _rwLock.EnterWriteLock();
        try
        {
            string key = slice.GetKey();
            Type stateType = slice.GetStateType();

            if (_slices.TryGetValue(key, out ISlice? existingSlice))
            {
                Type existingType = existingSlice.GetStateType();
                if (_slicesByStateType.TryGetValue(existingType, out ISlice? indexedSlice)
                    && ReferenceEquals(indexedSlice, existingSlice))
                {
                    _slicesByStateType[existingType] = slice;
                }
            }

            _slices[key] = slice;
            _slicesByStateType.TryAdd(stateType, slice);

            EventHandler handler = (sender, _) =>
            {
                if (sender is not ISlice updatedSlice) return;
                object newState = updatedSlice.GetState();
                SliceStateChanged?.Invoke(this, new StateChangedEventArgs(
                    updatedSlice.GetKey(), newState.GetType(), newState));
            };
            _sliceUpdateHandlers[slice.GetKey()] = handler;
            slice.StateUpdated += handler;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
```

Wrap read methods (e.g., `GetSlice<TState>`, `GetSliceByKey`, `TryGetSlice`, `HasSlice`, `GetStateDictionary`, etc.) in `_rwLock.EnterReadLock()` / `ExitReadLock()`.

Add `_rwLock.Dispose()` in the `Dispose` method.

- [ ] **Step 4: Add lock to SliceReducers lazy init**

In `SliceReducers.cs`, add a lock object:

```csharp
    private readonly object _initLock = new();
```

Update `GetState()` (lines 103-114):

```csharp
    public virtual object GetState()
    {
        if (!_isInitialized)
        {
            lock (_initLock)
            {
                if (!_isInitialized)
                {
                    _currentState = GetInitialState();
                    _isInitialized = true;
                }
            }
        }

        return _currentState is null
            ? throw new DuckyException($"State is null for slice '{GetKey()}'. Ensure GetInitialState() returns a non-null value.")
            : _currentState;
    }
```

- [ ] **Step 5: Update existing tests for new Dispatcher behavior**

Update `DispatcherTests.cs` — the Dispatcher no longer queues, so remove any tests that relied on queuing behavior. Add tests verifying:
- Dispatch fires ActionDispatched immediately
- Dispatch after Dispose throws DuckyException
- Concurrent dispatch from multiple threads doesn't corrupt state

- [ ] **Step 6: Write thread safety tests**

Create `src/tests/Ducky.Tests/Core/ThreadSafetyTests.cs`:

```csharp
namespace Ducky.Tests.Core;

public class ThreadSafetyTests
{
    [Fact]
    public async Task ConcurrentDispatch_ShouldNotCorruptState()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDucky(builder =>
        {
            builder.UseDefaultMiddlewares();
            builder.AddSlice<TestCounterReducers>();
        });
        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IStore>();
        await store.InitializeAsync();

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        const int iterations = 1000;

        // Act - dispatch from 10 threads simultaneously
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    dispatcher.Dispatch(new TestIncrement());
                }
            }));

        await Task.WhenAll(tasks);

        // Allow re-entrant queue to drain
        await Task.Delay(100);

        // Assert - count should be exactly 10 * iterations
        var state = store.GetSlice<TestCounterState>();
        state.Count.ShouldBe(10 * iterations);
    }
}

public record TestCounterState(int Count = 0);
public record TestIncrement;

public class TestCounterReducers : SliceReducers<TestCounterState>
{
    public TestCounterReducers()
    {
        On<TestIncrement>((state, _) => state with { Count = state.Count + 1 });
    }

    public override TestCounterState GetInitialState() => new();
}
```

- [ ] **Step 7: Run tests and verify**

Run: `dotnet test src/tests/Ducky.Tests --filter "FullyQualifiedName~Core" -v normal`
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/library/Ducky/Dispatcher.cs src/library/Ducky/DuckyStore.cs src/library/Ducky/ObservableSlices.cs src/library/Ducky/SliceReducers.cs src/tests/Ducky.Tests/
git commit -m "fix: unify dispatch re-entrancy and add thread-safety contract (#213, #214)"
```

---

### Task 2: ReactiveEffectBase IAsyncDisposable (US-03)

**Files:**
- Modify: `src/library/Ducky/Reactive/Base/ReactiveEffectBase.cs`
- Modify: `src/tests/Ducky.Tests/Reactive/ReactiveEffectBaseTests.cs` (or create if not exists)

- [ ] **Step 1: Write failing test for IAsyncDisposable**

```csharp
[Fact]
public async Task DisposeAsync_ShouldCallOnDisposeAsync_WithoutBlocking()
{
    // Arrange
    var effect = new TestAsyncDisposableEffect();

    // Act
    await ((IAsyncDisposable)effect).DisposeAsync();

    // Assert
    effect.DisposeAsyncCalled.ShouldBeTrue();
    effect.IsDisposed.ShouldBeTrue();
}

public class TestAsyncDisposableEffect : ReactiveEffectBase
{
    public bool DisposeAsyncCalled { get; private set; }

    protected override Task OnDisposeAsync()
    {
        DisposeAsyncCalled = true;
        return Task.CompletedTask;
    }

    protected override IObservable<object> HandleCore(
        IObservable<object> actions, IObservable<IStateProvider> stateProvider)
        => Observable.Empty<object>();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/tests/Ducky.Tests --filter "DisposeAsync_ShouldCallOnDisposeAsync" -v normal`
Expected: FAIL — ReactiveEffectBase doesn't implement IAsyncDisposable.

- [ ] **Step 3: Implement IAsyncDisposable on ReactiveEffectBase**

Replace the `Dispose(bool)` method and add `IAsyncDisposable`:

```csharp
public abstract class ReactiveEffectBase : ReactiveEffect, IDisposable, IAsyncDisposable
{
    // ... existing fields ...

    /// <summary>
    /// Asynchronously disposes the effect and performs cleanup.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await OnDisposeAsync().ConfigureAwait(false);
        _disposables.Dispose();
        _errors.OnCompleted();
        _errors.Dispose();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Synchronous dispose fallback. Prefer DisposeAsync().
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            // Do NOT call OnDisposeAsync().GetAwaiter().GetResult() — deadlock risk.
            // Synchronous dispose only cleans up synchronous resources.
            _disposables.Dispose();
            _errors.OnCompleted();
            _errors.Dispose();
        }

        _isDisposed = true;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test src/tests/Ducky.Tests --filter "FullyQualifiedName~ReactiveEffect" -v normal`
Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add src/library/Ducky/Reactive/Base/ReactiveEffectBase.cs src/tests/Ducky.Tests/
git commit -m "fix: implement IAsyncDisposable on ReactiveEffectBase to prevent deadlock (#215)"
```

---

### Task 3: StateSnapshot O(1) Lookups (US-04)

**Files:**
- Modify: `src/library/Ducky/Reactive/Middlewares/ReactiveEffects/StateSnapshot.cs`
- Modify: `src/library/Ducky/ObservableSlices.cs`

- [ ] **Step 1: Write failing test for O(1) snapshot lookup**

Create or update `src/tests/Ducky.Tests/Core/StateSnapshotTests.cs`:

```csharp
[Fact]
public void GetSlice_WithTypeIndex_ShouldBeO1()
{
    // Arrange — create snapshot with type index
    var dict = ImmutableSortedDictionary.CreateBuilder<string, object>();
    dict.Add("counter", new TestCounterState(42));
    dict.Add("todo", new TestTodoState("items"));
    var typeIndex = new Dictionary<Type, string>
    {
        [typeof(TestCounterState)] = "counter",
        [typeof(TestTodoState)] = "todo"
    };
    var snapshot = new StateSnapshot(dict.ToImmutable(), typeIndex);

    // Act
    var counter = snapshot.GetSlice<TestCounterState>();

    // Assert
    counter.Count.ShouldBe(42);
}

public record TestTodoState(string Items);
```

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — StateSnapshot constructor doesn't accept type index.

- [ ] **Step 3: Update StateSnapshot with type-indexed dictionary**

Replace `StateSnapshot.cs`:

```csharp
internal sealed class StateSnapshot : IStateProvider
{
    private readonly ImmutableSortedDictionary<string, object> _state;
    private readonly Dictionary<Type, string> _typeIndex;

    public StateSnapshot(
        ImmutableSortedDictionary<string, object> state,
        Dictionary<Type, string> typeIndex)
    {
        _state = state;
        _typeIndex = typeIndex;
    }

    public TState GetSlice<TState>()
    {
        if (_typeIndex.TryGetValue(typeof(TState), out string? key)
            && _state.TryGetValue(key, out object? value))
        {
            return (TState)value;
        }

        throw new InvalidOperationException(
            $"Slice of type {typeof(TState).Name} not found in state snapshot.");
    }

    public TState GetSliceByKey<TState>(string key)
    {
        if (_state.TryGetValue(key, out object? value) && value is TState state)
        {
            return state;
        }

        throw new KeyNotFoundException($"Slice with key '{key}' not found in state snapshot.");
    }

    public bool TryGetSlice<TState>(out TState? state)
    {
        if (_typeIndex.TryGetValue(typeof(TState), out string? key)
            && _state.TryGetValue(key, out object? value))
        {
            state = (TState)value;
            return true;
        }

        state = default;
        return false;
    }

    public bool HasSlice<TState>()
    {
        return _typeIndex.ContainsKey(typeof(TState));
    }

    public bool HasSliceByKey(string key) => _state.ContainsKey(key);

    public IReadOnlyCollection<string> GetSliceKeys() => _state.Keys.ToList();

    public IReadOnlyDictionary<string, object> GetAllSlices() => _state;

    public ImmutableSortedDictionary<string, object> GetStateDictionary() => _state;

    public ImmutableSortedSet<string> GetKeys() => _state.Keys.ToImmutableSortedSet();
}
```

- [ ] **Step 4: Add cached state dictionary and type index to ObservableSlices**

Add to `ObservableSlices`:

```csharp
    private ImmutableSortedDictionary<string, object>? _cachedStateDictionary;
    private Dictionary<Type, string>? _cachedTypeIndex;
    private volatile bool _stateDirty = true;
```

Invalidate cache when slice state changes — in the handler created in `AddSlice`:

```csharp
    EventHandler handler = (sender, _) =>
    {
        _stateDirty = true;
        // ... existing event publishing ...
    };
```

Add a method to get the snapshot data:

```csharp
    public (ImmutableSortedDictionary<string, object> State, Dictionary<Type, string> TypeIndex) GetSnapshotData()
    {
        _rwLock.EnterReadLock();
        try
        {
            if (_stateDirty || _cachedStateDictionary is null)
            {
                _cachedStateDictionary = _slices.ToImmutableSortedDictionary(
                    kvp => kvp.Key, kvp => kvp.Value.GetState());
                _cachedTypeIndex = _slicesByStateType.ToDictionary(
                    kvp => kvp.Key, kvp => kvp.Value.GetKey());
                _stateDirty = false;
            }

            return (_cachedStateDictionary, _cachedTypeIndex);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }
```

- [ ] **Step 5: Update ReactiveEffectMiddleware to use new snapshot constructor**

Find where `StateSnapshot` is constructed in `ReactiveEffectMiddleware.AfterReduce` and update it to pass the type index:

```csharp
    var (stateDict, typeIndex) = _store.GetSnapshotData();
    var snapshot = new StateSnapshot(stateDict, typeIndex);
```

This requires `IStore` to expose `GetSnapshotData()` or the middleware to access `ObservableSlices` directly. The simplest approach: add `GetSnapshotData()` to `IStateProvider` or cast internally.

- [ ] **Step 6: Run all tests**

Run: `dotnet test src/tests/Ducky.Tests -v normal`
Expected: All pass.

- [ ] **Step 7: Commit**

```bash
git add src/library/Ducky/ src/tests/Ducky.Tests/
git commit -m "perf: O(1) StateSnapshot lookups with type-indexed dictionary (#216)"
```

---

### Task 4: MemoizedSelector Zero-Alloc Cache Hit (US-05)

**Files:**
- Modify: `src/library/Ducky/Selectors/MemoizedSelector.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public void Create_SameStateReference_ShouldNotAllocateDependencyArray()
{
    // Arrange
    int dependencyCallCount = 0;
    var selector = MemoizedSelector.Create<TestCounterState, int>(
        state => state.Count * 2,
        state => { dependencyCallCount++; return state.Count; });

    var state = new TestCounterState(5);

    // Act — first call populates cache
    selector(state);
    int firstCallCount = dependencyCallCount;

    // Second call with same reference should short-circuit
    selector(state);

    // Assert
    dependencyCallCount.ShouldBe(firstCallCount); // No additional dependency calls
}
```

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — dependency function called again on cache hit.

- [ ] **Step 3: Add reference equality short-circuit**

Replace the `Create` method's lambda (lines 31-48) in `MemoizedSelector.cs`:

```csharp
        return state =>
        {
            // Fast path: if state reference hasn't changed, return cached result
            if (hasCache && ReferenceEquals(lastState, state))
            {
                return lastResult!;
            }

            object[] currentDependencies = Array.ConvertAll(dependencies, dep => dep(state));

            if (hasCache
                && EqualityComparer<TState>.Default.Equals(lastState!, state)
                && AreDependenciesEqual(lastDependencies!, currentDependencies))
            {
                lastState = state; // Update reference for future fast-path
                return lastResult!;
            }

            TResult result = selector(state);
            lastState = state;
            lastResult = result;
            lastDependencies = currentDependencies;
            hasCache = true;
            return result;
        };
```

Also fix `AreDependenciesEqual` to avoid LINQ allocation:

```csharp
    private static bool AreDependenciesEqual(object[] oldDeps, object[] newDeps)
    {
        if (oldDeps.Length != newDeps.Length)
        {
            return false;
        }

        for (int i = 0; i < oldDeps.Length; i++)
        {
            if (!Equals(oldDeps[i], newDeps[i]))
            {
                return false;
            }
        }

        return true;
    }
```

- [ ] **Step 4: Run tests**

Run: `dotnet test src/tests/Ducky.Tests --filter "FullyQualifiedName~Selector" -v normal`
Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add src/library/Ducky/Selectors/MemoizedSelector.cs src/tests/Ducky.Tests/
git commit -m "perf: zero-alloc MemoizedSelector cache hit with reference equality (#217)"
```

---

### Task 5: Async Effect Error Surfacing (US-07)

**Files:**
- Modify: `src/library/Ducky/Middlewares/AsyncEffect/AsyncEffectMiddleware.cs`
- Create: `src/library/Ducky/Middlewares/AsyncEffect/EffectOptions.cs`

- [ ] **Step 1: Create EffectOptions**

Create `src/library/Ducky/Middlewares/AsyncEffect/EffectOptions.cs`:

```csharp
// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Configuration options for async effect middleware.
/// </summary>
public class EffectOptions
{
    /// <summary>
    /// When true, re-throws effect exceptions after logging. Useful in development.
    /// Default: false.
    /// </summary>
    public bool ThrowOnEffectError { get; set; }
}
```

- [ ] **Step 2: Add ILogger to AsyncEffectMiddleware**

Update constructor to accept `ILogger<AsyncEffectMiddleware>` and `EffectOptions`:

```csharp
    private readonly ILogger<AsyncEffectMiddleware> _logger;
    private readonly EffectOptions _options;

    public AsyncEffectMiddleware(
        IEnumerable<IAsyncEffect> effects,
        IStoreEventPublisher eventPublisher,
        ILogger<AsyncEffectMiddleware> logger,
        EffectOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(logger);

        _effects = effects;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _options = options ?? new EffectOptions();
    }
```

- [ ] **Step 3: Add ILogger.LogError to error publishing**

In `TriggerEffects`, update both sync and async error paths:

For the synchronous catch (line 108-111):
```csharp
            catch (Exception exception)
            {
                _logger.LogError(exception, "Async effect {EffectType} failed synchronously for action {ActionType}.",
                    effect.GetType().Name, action.GetType().Name);
                _eventPublisher.Publish(new EffectErrorEventArgs(exception, effect.GetType(), action));
            }
```

For the async error loop (lines 133-141):
```csharp
                foreach ((IAsyncEffect effect, Task task) in effectTasks)
                {
                    if (task is { IsFaulted: true, Exception: not null })
                    {
                        foreach (Exception innerEx in task.Exception.Flatten().InnerExceptions)
                        {
                            _logger.LogError(innerEx,
                                "Async effect {EffectType} failed for action {ActionType}.",
                                effect.GetType().Name, action.GetType().Name);
                            _eventPublisher.Publish(new EffectErrorEventArgs(innerEx, effect.GetType(), action));
                        }
                    }
                }
```

- [ ] **Step 4: Run tests and fix any DI registration issues**

Run: `dotnet test src/tests/Ducky.Tests --filter "FullyQualifiedName~AsyncEffect" -v normal`

If tests fail due to missing `ILogger` in constructors, update test setups to provide `NullLogger<AsyncEffectMiddleware>.Instance`.

- [ ] **Step 5: Commit**

```bash
git add src/library/Ducky/Middlewares/AsyncEffect/ src/tests/Ducky.Tests/
git commit -m "fix: surface async effect errors via ILogger instead of silent event publishing (#218)"
```

---

### Task 6: Run full test suite and verify Layer 1

- [ ] **Step 1: Run all tests**

Run: `dotnet test -v normal`
Expected: All tests pass across Ducky.Tests and Ducky.Blazor.Tests.

- [ ] **Step 2: Fix any compilation or test failures**

If the DuckyStore constructor change (added `ILogger<DuckyStore>`) breaks Blazor tests or DI registrations, update `DuckyBuilder.Build()` to register the logger and update any test that constructs DuckyStore directly.

- [ ] **Step 3: Final commit for Layer 1**

```bash
git add -A
git commit -m "chore: fix remaining test failures after Layer 1 core foundation changes"
```
