# Layer 4: DX Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Polish developer experience — consolidate DI entry points, improve error messages, fix async void handlers, add persistence warnings, and eliminate two-phase initialization.

**Architecture:** All five tasks are independent and can be parallelized. Each is a self-contained improvement.

**Tech Stack:** .NET 10, C# 13, Blazor WebAssembly, xUnit

**Spec:** `docs/superpowers/specs/2026-03-26-legends-review-user-stories-design.md`

**Issues:** #224, #225, #226, #227, #228

**Prerequisite:** Layers 1, 2, and 3 must be complete.

---

### Task 1: Consolidate to Two DI Entry Points (US-13)

**Files:**
- Modify: `src/library/Ducky/DuckyServiceCollectionExtensions.cs`
- Modify: `src/library/Ducky.Blazor/DuckyBlazorServiceCollectionExtensions.cs`
- Modify: all demos and tests that reference removed methods

- [ ] **Step 1: Identify all DI registration methods to remove**

Search for: `AddDuckyStore`, `AddDuckyWithMiddleware`, `AddDuckyWithPipeline`, `AddDuckyStoreWithReactiveEffects`

Run: `grep -rn "AddDuckyStore\|AddDuckyWithMiddleware\|AddDuckyWithPipeline\|AddDuckyStoreWithReactiveEffects" src/`

Note all call sites that need updating.

- [ ] **Step 2: Remove deprecated methods from DuckyServiceCollectionExtensions.cs**

Keep only `AddDucky(this IServiceCollection services, Action<DuckyBuilder> configure)`. Delete all other overloads. If there are backward-compat shims, delete those too.

- [ ] **Step 3: Remove deprecated methods from DuckyBlazorServiceCollectionExtensions.cs**

Keep only `AddDuckyBlazor(this IServiceCollection services, Action<BlazorDuckyBuilder> configure)`. Delete other overloads.

- [ ] **Step 4: Update all call sites in demos and tests**

Replace all references to removed methods with the appropriate remaining method. For example:
- `services.AddDuckyStore(builder => ...)` → `services.AddDucky(builder => ...)`

- [ ] **Step 5: Run all tests**

Run: `dotnet test -v normal`
Expected: All pass.

- [ ] **Step 6: Commit**

```bash
git add src/
git commit -m "refactor: consolidate to two DI entry points — AddDucky and AddDuckyBlazor (#224)"
```

---

### Task 2: Actionable Error Messages (US-14)

**Files:**
- Create: `src/library/Ducky/ExceptionFactory.cs`
- Modify: `src/library/Ducky/SliceReducers.cs`
- Modify: `src/library/Ducky/ObservableSlices.cs`
- Modify: `src/library/Ducky/Normalization/NormalizedState.cs`
- Modify: `src/library/Ducky/DuckyStore.cs`
- Modify: `src/library/Ducky/Dispatcher.cs`

- [ ] **Step 1: Create ExceptionFactory**

Create `src/library/Ducky/ExceptionFactory.cs`:

```csharp
// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Centralized factory for creating descriptive exceptions.
/// Each exception answers: what happened, why, and what to do.
/// </summary>
internal static class ExceptionFactory
{
    public static DuckyException SliceNotFound(Type stateType)
        => new($"Slice of type '{stateType.Name}' not found. Did you register it with builder.AddSlice<YourReducers>()? Available slices can be checked with store.HasSlice<{stateType.Name}>().");

    public static KeyNotFoundException SliceKeyNotFound(string key)
        => new($"Slice with key '{key}' not found. Verify the slice key matches the kebab-case name derived from your reducer class name.");

    public static DuckyException StateIsNull(string sliceKey)
        => new($"State for slice '{sliceKey}' is null. Ensure the slice reducer returns a non-null initial state from GetInitialState().");

    public static InvalidOperationException ReentrantDepthExceeded(int maxDepth, string? currentAction, string? newAction)
        => new($"Action '{newAction}' aborted: re-entrant dispatch depth exceeded maximum of {maxDepth}. This usually means a reducer or effect is dispatching in a loop. Check the '{currentAction}' -> '{newAction}' dispatch chain.");

    public static DuckyException DispatcherDisposed()
        => new("Cannot dispatch to a disposed Dispatcher. Ensure components unsubscribe in Dispose().",
            new ObjectDisposedException(nameof(Dispatcher)));

    public static DuckyException StoreDisposed()
        => new("Cannot dispatch to a disposed DuckyStore. This usually happens when a component dispatches after navigation. Ensure components unsubscribe in Dispose().");

    public static KeyNotFoundException EntityNotFound<TKey>(TKey key, Type entityType)
        => new($"Entity with key '{key}' not found in NormalizedState<{typeof(TKey).Name}, {entityType.Name}>. Call HasEntity() before accessing, or use TryGetEntity() for safe access.");
}
```

- [ ] **Step 2: Update all throw sites to use ExceptionFactory**

In `SliceReducers.cs` line 112:
```csharp
// Before: throw new DuckyException("State is null.");
// After:
throw ExceptionFactory.StateIsNull(GetKey());
```

In `ObservableSlices.cs` line 106-107:
```csharp
// Before: throw new InvalidOperationException($"Slice of type {typeof(TState).Name} not found.");
// After:
throw ExceptionFactory.SliceNotFound(typeof(TState));
```

In `Dispatcher.cs` line 33-35:
```csharp
// Before: throw new DuckyException("The dispatcher has been disposed.", ...);
// After:
throw ExceptionFactory.DispatcherDisposed();
```

In `NormalizedState.cs`, replace entity-not-found exceptions with `ExceptionFactory.EntityNotFound(key, typeof(TEntity))`.

- [ ] **Step 3: Run tests**

Run: `dotnet test -v normal`
Expected: All pass. Some tests may assert on exact exception messages — update those to match new messages.

- [ ] **Step 4: Commit**

```bash
git add src/library/Ducky/
git commit -m "feat: actionable error messages with ExceptionFactory (#225)"
```

---

### Task 3: DuckyComponent Catch-All (US-15)

**Files:**
- Modify: `src/library/Ducky.Blazor/Components/DuckyComponent.cs`
- Modify: `src/library/Ducky.Blazor/Components/DuckySelectorComponent.cs`

- [ ] **Step 1: Update DuckyComponent.OnStateChanged**

Find the `catch (ObjectDisposedException)` block and replace with:

```csharp
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error in DuckyComponent<{StateType}>.OnStateChanged while processing state change.",
            typeof(TState).Name);
    }
```

- [ ] **Step 2: Update DuckySelectorComponent.OnStateChanged**

Same pattern — replace `catch (ObjectDisposedException)` with `catch (Exception ex)` and log.

- [ ] **Step 3: Run Blazor tests**

Run: `dotnet test src/tests/Ducky.Blazor.Tests --filter "FullyQualifiedName~Component" -v normal`
Expected: All pass.

- [ ] **Step 4: Commit**

```bash
git add src/library/Ducky.Blazor/Components/
git commit -m "fix: catch all exceptions in DuckyComponent async void handlers (#226)"
```

---

### Task 4: PersistenceInitializer Missing Warning (US-16)

**Files:**
- Modify: `src/library/Ducky.Blazor/Middlewares/Persistence/PersistenceMiddleware.cs`
- Modify: `src/library/Ducky.Blazor/Middlewares/Persistence/PersistenceOptions.cs`

- [ ] **Step 1: Add HydrationTimeoutWarningMs to PersistenceOptions**

In `PersistenceOptions.cs`, add:

```csharp
    /// <summary>
    /// Timeout in milliseconds after which a warning is logged if hydration hasn't started.
    /// Set to 0 to disable. Default: 5000 (5 seconds).
    /// </summary>
    public int HydrationTimeoutWarningMs { get; set; } = 5000;
```

- [ ] **Step 2: Add hydration timeout timer to PersistenceMiddleware**

In `PersistenceMiddleware.InitializeAsync()`, after initialization, start a warning timer:

```csharp
    if (_options.HydrationTimeoutWarningMs > 0)
    {
        _hydrationWarningTimer = new Timer(
            _ =>
            {
                if (!_hydrationManager.IsHydrated)
                {
                    _logger.LogWarning(
                        "Persistence is configured but HydrateAsync() has not been called after {Timeout}ms. " +
                        "Did you forget to add <PersistenceInitializer> to your Blazor layout?",
                        _options.HydrationTimeoutWarningMs);
                }
            },
            null,
            _options.HydrationTimeoutWarningMs,
            Timeout.Infinite);
    }
```

Add field: `private Timer? _hydrationWarningTimer;`

Cancel the timer when hydration starts (in `BeforeReduce` when detecting `HydrationStartedAction`):

```csharp
    _hydrationWarningTimer?.Dispose();
    _hydrationWarningTimer = null;
```

- [ ] **Step 3: Run tests**

Run: `dotnet test src/tests/Ducky.Blazor.Tests --filter "FullyQualifiedName~Persistence" -v normal`
Expected: All pass.

- [ ] **Step 4: Commit**

```bash
git add src/library/Ducky.Blazor/Middlewares/Persistence/
git commit -m "feat: warn when persistence configured but PersistenceInitializer missing (#227)"
```

---

### Task 5: Eliminate Two-Phase Initialization (US-17)

**Files:**
- Modify: `src/library/Ducky/Builder/DuckyBuilder.cs`
- Modify: `src/library/Ducky/DuckyStore.cs`
- Create: `src/library/Ducky.Blazor/Services/DuckyStoreAsyncInitializer.cs`
- Modify: `src/library/Ducky.Blazor/Services/DuckyStoreInitializer.cs`

- [ ] **Step 1: Split DuckyStore.InitializeAsync into sync + async parts**

In `DuckyStore.cs`, extract the synchronous parts of `InitializeAsync` into `InitializeSynchronous()`:

```csharp
    /// <summary>
    /// Performs synchronous initialization — registers slices and subscribes to dispatch.
    /// Called automatically by the DI factory.
    /// </summary>
    internal void InitializeSynchronous()
    {
        if (IsInitialized) return;

        // Subscribe to action stream
        _dispatcher.ActionDispatched += OnActionDispatched;
    }

    /// <summary>
    /// Initializes async components (middleware pipeline).
    /// </summary>
    [Obsolete("Store is now initialized automatically via DI. Use DuckyStoreAsyncInitializer for async middleware init.")]
    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        InitializeSynchronous();
        await _pipeline.InitializeAsync(_dispatcher, this).ConfigureAwait(false);
        _dispatcher.Dispatch(new StoreInitialized());
        _eventPublisher.Publish(new StoreInitializedEventArgs(_sliceKeys.Count, _sliceKeys));
        IsInitialized = true;
    }

    /// <summary>
    /// Initializes the middleware pipeline asynchronously.
    /// </summary>
    internal async Task InitializePipelineAsync()
    {
        await _pipeline.InitializeAsync(_dispatcher, this).ConfigureAwait(false);
        _dispatcher.Dispatch(new StoreInitialized());
        _eventPublisher.Publish(new StoreInitializedEventArgs(_sliceKeys.Count, _sliceKeys));
        IsInitialized = true;
    }
```

- [ ] **Step 2: Update DuckyBuilder.Build() to call sync init in factory**

In the DI registration within `DuckyBuilder.Build()`:

```csharp
services.AddSingleton<IStore>(sp =>
{
    var store = new DuckyStore(
        sp.GetRequiredService<IDispatcher>(),
        sp.GetRequiredService<ActionPipeline>(),
        sp.GetRequiredService<IStoreEventPublisher>(),
        sp.GetServices<ISlice>(),
        sp.GetRequiredService<ILogger<DuckyStore>>());

    store.InitializeSynchronous();
    return store;
});
```

- [ ] **Step 3: Create DuckyStoreAsyncInitializer hosted service**

Create `src/library/Ducky.Blazor/Services/DuckyStoreAsyncInitializer.cs`:

```csharp
// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Blazor.Services;

/// <summary>
/// Hosted service that initializes the DuckyStore middleware pipeline asynchronously.
/// Registered automatically by AddDuckyBlazor().
/// </summary>
public class DuckyStoreAsyncInitializer : IHostedService
{
    private readonly IStore _store;
    private readonly ILogger<DuckyStoreAsyncInitializer> _logger;

    public DuckyStoreAsyncInitializer(
        IStore store,
        ILogger<DuckyStoreAsyncInitializer> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Initializing DuckyStore middleware pipeline...");

        if (_store is DuckyStore duckyStore)
        {
            await duckyStore.InitializePipelineAsync();
        }

        _logger.LogDebug("DuckyStore middleware pipeline initialized.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

- [ ] **Step 4: Register hosted service in AddDuckyBlazor**

In `DuckyBlazorServiceCollectionExtensions.cs`, add:

```csharp
services.AddHostedService<DuckyStoreAsyncInitializer>();
```

- [ ] **Step 5: Update tests that call InitializeAsync directly**

Search for `InitializeAsync()` calls in tests. For most, they can remain (the method still works, just marked obsolete). Suppress the obsolete warning in test projects if needed:

```xml
<NoWarn>CS0618</NoWarn>
```

- [ ] **Step 6: Run all tests**

Run: `dotnet test -v normal`
Expected: All pass.

- [ ] **Step 7: Commit**

```bash
git add src/library/Ducky/ src/library/Ducky.Blazor/ src/tests/
git commit -m "refactor: eliminate two-phase initialization — store usable after DI resolution (#228)"
```

---

### Task 6: Final verification

- [ ] **Step 1: Run full test suite**

Run: `dotnet test -v normal`
Expected: All tests pass.

- [ ] **Step 2: Build demo apps**

Run: `dotnet build src/demo/Demo.BlazorWasm`
Run: `dotnet build src/demo/Demo.ConsoleApp`
Expected: Both build with no errors (may have obsolete warnings — that's expected).

- [ ] **Step 3: Final commit**

```bash
git add -A
git commit -m "chore: Layer 4 DX polish complete — all 17 user stories implemented"
```
