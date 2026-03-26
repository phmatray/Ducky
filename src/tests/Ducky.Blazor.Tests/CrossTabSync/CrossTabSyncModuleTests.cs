// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Blazor.CrossTabSync;
using Ducky.Blazor.Middlewares.Persistence;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Tests.CrossTabSync;

public class CrossTabSyncModuleTests : IAsyncDisposable
{
    private readonly IEnhancedPersistenceProvider<Dictionary<string, object>> _persistenceProvider;
    private readonly IDispatcher _dispatcher;
    private readonly CrossTabSyncOptions _options;
    private readonly ILogger<CrossTabSyncModule> _logger;

    public CrossTabSyncModuleTests()
    {
        _persistenceProvider = A.Fake<IEnhancedPersistenceProvider<Dictionary<string, object>>>();
        _dispatcher = A.Fake<IDispatcher>();
        _options = new CrossTabSyncOptions();
        _logger = A.Fake<ILogger<CrossTabSyncModule>>();
    }

    public async ValueTask DisposeAsync()
    {
        // No module to dispose in base — each test creates its own if needed
        await Task.CompletedTask;
    }

    [Fact]
    public async Task OnExternalStateChangedAsync_WhenDisabled_DoesNothing()
    {
        // Arrange
        _options.Enabled = false;

        // Act
        await InvokeOnExternalStateChanged(_options.StorageKey);

        // Assert
        A.CallTo(() => _persistenceProvider.LoadWithMetadataAsync(A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task OnExternalStateChangedAsync_WithDifferentKey_DoesNothing()
    {
        // Arrange
        _options.StorageKey = "ducky:state";

        // Act — pass a key that doesn't match
        await InvokeOnExternalStateChanged("some-other-key");

        // Assert
        A.CallTo(() => _persistenceProvider.LoadWithMetadataAsync(A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task OnExternalStateChangedAsync_WithMatchingKey_LoadsAndDispatchesSlices()
    {
        // Arrange
        var testState = new Dictionary<string, object>
        {
            ["counter"] = new { Count = 42 },
            ["todos"] = new { Items = new[] { "task1" } }
        };

        var container = new PersistedStateContainer<Dictionary<string, object>>
        {
            State = testState,
            Metadata = new PersistenceMetadata
            {
                Version = 1,
                Timestamp = DateTime.UtcNow
            }
        };

        A.CallTo(() => _persistenceProvider.LoadWithMetadataAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<PersistedStateContainer<Dictionary<string, object>>?>(container));

        // Act
        await InvokeOnExternalStateChanged(_options.StorageKey);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(
            A<HydrateSliceAction>.That.Matches(a => a.SliceKey == "counter")))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _dispatcher.Dispatch(
            A<HydrateSliceAction>.That.Matches(a => a.SliceKey == "todos")))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task OnExternalStateChangedAsync_WithIncludedSliceKeys_OnlyHydratesMatchingSlices()
    {
        // Arrange
        _options.IncludedSliceKeys = ["counter"];

        var testState = new Dictionary<string, object>
        {
            ["counter"] = new { Count = 10 },
            ["todos"] = new { Items = new[] { "excluded" } }
        };

        var container = new PersistedStateContainer<Dictionary<string, object>>
        {
            State = testState,
            Metadata = new PersistenceMetadata { Version = 1, Timestamp = DateTime.UtcNow }
        };

        A.CallTo(() => _persistenceProvider.LoadWithMetadataAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<PersistedStateContainer<Dictionary<string, object>>?>(container));

        // Act
        await InvokeOnExternalStateChanged(_options.StorageKey);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(
            A<HydrateSliceAction>.That.Matches(a => a.SliceKey == "counter")))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _dispatcher.Dispatch(
            A<HydrateSliceAction>.That.Matches(a => a.SliceKey == "todos")))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task OnExternalStateChangedAsync_WithNullPersistedState_DoesNotDispatch()
    {
        // Arrange
        A.CallTo(() => _persistenceProvider.LoadWithMetadataAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<PersistedStateContainer<Dictionary<string, object>>?>(null));

        // Act
        await InvokeOnExternalStateChanged(_options.StorageKey);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(A<HydrateSliceAction>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task OnExternalStateChangedAsync_WithNullChangedKey_StillHydrates()
    {
        // Arrange — null key should still trigger hydration (backwards compat)
        var testState = new Dictionary<string, object> { ["counter"] = new { Count = 1 } };

        var container = new PersistedStateContainer<Dictionary<string, object>>
        {
            State = testState,
            Metadata = new PersistenceMetadata { Version = 1, Timestamp = DateTime.UtcNow }
        };

        A.CallTo(() => _persistenceProvider.LoadWithMetadataAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<PersistedStateContainer<Dictionary<string, object>>?>(container));

        // Act
        await InvokeOnExternalStateChanged(null);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(
            A<HydrateSliceAction>.That.Matches(a => a.SliceKey == "counter")))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task OnExternalStateChangedAsync_WhenProviderThrows_DoesNotCrash()
    {
        // Arrange
        A.CallTo(() => _persistenceProvider.LoadWithMetadataAsync(A<CancellationToken>._))
            .Throws(new Exception("Storage unavailable"));

        // Act — should not throw
        await InvokeOnExternalStateChanged(_options.StorageKey);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(A<HydrateSliceAction>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task OnExternalStateChangedAsync_Debounces_MultipleRapidCalls()
    {
        // Arrange
        _options.DebounceMs = 200;

        var testState = new Dictionary<string, object> { ["counter"] = new { Count = 99 } };

        var container = new PersistedStateContainer<Dictionary<string, object>>
        {
            State = testState,
            Metadata = new PersistenceMetadata { Version = 1, Timestamp = DateTime.UtcNow }
        };

        A.CallTo(() => _persistenceProvider.LoadWithMetadataAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<PersistedStateContainer<Dictionary<string, object>>?>(container));

        // Act — fire multiple rapid calls, only the last should trigger hydration
        await InvokeOnExternalStateChangedWithoutWait(_options.StorageKey);
        await InvokeOnExternalStateChangedWithoutWait(_options.StorageKey);
        await InvokeOnExternalStateChangedWithoutWait(_options.StorageKey);

        // Wait for debounce to fire
        await Task.Delay(400);

        // Assert — should only have loaded once due to debounce
        A.CallTo(() => _persistenceProvider.LoadWithMetadataAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    /// <summary>
    /// Helper to invoke the JSInvokable method directly and wait for debounce + async hydration to complete.
    /// We bypass JSInterop by calling the method directly on a module created with a fake JS runtime.
    /// </summary>
    private async Task InvokeOnExternalStateChanged(string? changedKey)
    {
        CrossTabSyncModule module = CreateModule();
        await module.OnExternalStateChangedAsync(changedKey);

        // Wait for debounce timer + async hydration to complete
        await Task.Delay(_options.DebounceMs + 200);

        await module.DisposeAsync();
    }

    /// <summary>
    /// Invoke without waiting — used for debounce testing.
    /// </summary>
    private Task InvokeOnExternalStateChangedWithoutWait(string? changedKey)
    {
        // Use a shared module for debounce test — reuse the same instance
        _sharedModule ??= CreateModule();
        return _sharedModule.OnExternalStateChangedAsync(changedKey);
    }

    private CrossTabSyncModule? _sharedModule;

    private CrossTabSyncModule CreateModule()
    {
        Microsoft.JSInterop.IJSRuntime jsRuntime = A.Fake<Microsoft.JSInterop.IJSRuntime>();
        return new CrossTabSyncModule(jsRuntime, _persistenceProvider, _dispatcher, _options, _logger);
    }
}
