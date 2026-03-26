// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Ducky.Blazor.Middlewares.DevTools;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Middlewares.DevTools;

public class TimeTravelTests
{
    private readonly IStore _store;
    private readonly IDispatcher _dispatcher;
    private readonly DevToolsStateManager _stateManager;
    private readonly DevToolsMiddleware _middleware;
    private readonly DevToolsOptions _options;

    public TimeTravelTests()
    {
        IJSRuntime jsRuntime = A.Fake<IJSRuntime>();
        _stateManager = new DevToolsStateManager(A.Fake<ILogger<DevToolsStateManager>>());
        _options = new DevToolsOptions { MaxAge = 50 };

        ReduxDevToolsModule devTools = new(
            jsRuntime,
            _stateManager,
            A.Fake<ILogger<ReduxDevToolsModule>>(),
            _options);

        _middleware = new DevToolsMiddleware(
            devTools,
            _stateManager,
            A.Fake<ILogger<DevToolsMiddleware>>(),
            _options);

        _store = A.Fake<IStore>();
        _dispatcher = A.Fake<IDispatcher>();

        // Set up store to return serializable state
        ImmutableSortedDictionary<string, object> initialState = ImmutableSortedDictionary<string, object>.Empty
            .Add("counter", 0);

        A.CallTo(() => _store.GetStateDictionary()).Returns(initialState);
        A.CallTo(() => _store.GetAllSlices())
            .Returns(new Dictionary<string, object> { ["counter"] = 0 }.AsReadOnly());

        _stateManager.SetInitialState(initialState);

        // Initialize middleware
        _middleware.InitializeAsync(_dispatcher, _store);
    }

    [Fact]
    public void AfterReduce_RecordsHistoryEntry()
    {
        // Arrange
        object action = new TestAction("increment");

        // Act
        _middleware.AfterReduce(action);

        // Assert
        _middleware.History.Count.ShouldBe(1);
        _middleware.History[0].Action.ShouldBe(action);
        _middleware.History[0].IsSkipped.ShouldBeFalse();
    }

    [Fact]
    public void AfterReduce_DoesNotRecordWhenPaused()
    {
        // Arrange
        _middleware.SetRecording(isPaused: true);

        // Act
        _middleware.AfterReduce(new TestAction("increment"));

        // Assert
        _middleware.History.Count.ShouldBe(0);
    }

    [Fact]
    public void AfterReduce_ResumesRecordingAfterUnpause()
    {
        // Arrange
        _middleware.SetRecording(isPaused: true);
        _middleware.AfterReduce(new TestAction("skipped"));

        // Act
        _middleware.SetRecording(isPaused: false);
        _middleware.AfterReduce(new TestAction("recorded"));

        // Assert
        _middleware.History.Count.ShouldBe(1);
    }

    [Fact]
    public void JumpToAction_RestoresCorrectState()
    {
        // Arrange - dispatch two actions to build history
        _middleware.AfterReduce(new TestAction("first"));
        int firstSeqNum = _middleware.History[0].SequenceNumber;

        // Update store state for second action
        ImmutableSortedDictionary<string, object> secondState = ImmutableSortedDictionary<string, object>.Empty
            .Add("counter", 1);

        A.CallTo(() => _store.GetStateDictionary()).Returns(secondState);

        _middleware.AfterReduce(new TestAction("second"));

        // Act - jump back to first action
        _middleware.JumpToAction(firstSeqNum);

        // Assert - RestoreState should have been called
        A.CallTo(() => _store.RestoreState(A<IReadOnlyDictionary<string, object>>._))
            .MustHaveHappened();
    }

    [Fact]
    public void ToggleAction_ReplaysWithoutSkippedAction()
    {
        // Arrange
        _middleware.AfterReduce(new TestAction("first"));
        int firstSeqNum = _middleware.History[0].SequenceNumber;
        _middleware.AfterReduce(new TestAction("second"));

        // Act - toggle first action (skip it)
        _middleware.ToggleAction(firstSeqNum);

        // Assert
        _middleware.History[0].IsSkipped.ShouldBeTrue();
        _middleware.History[1].IsSkipped.ShouldBeFalse();

        // RestoreState should have been called (initial + replay)
        A.CallTo(() => _store.RestoreState(A<IReadOnlyDictionary<string, object>>._))
            .MustHaveHappened();
    }

    [Fact]
    public void SweepSkippedActions_RemovesSkippedEntries()
    {
        // Arrange
        _middleware.AfterReduce(new TestAction("first"));
        int firstSeqNum = _middleware.History[0].SequenceNumber;
        _middleware.AfterReduce(new TestAction("second"));
        _middleware.AfterReduce(new TestAction("third"));

        // Toggle first action to skip it
        _middleware.ToggleAction(firstSeqNum);
        _middleware.History.Count.ShouldBe(3);

        // Reset call tracking
        Fake.ClearRecordedCalls(_store);

        // Act
        _middleware.SweepSkippedActions();

        // Assert - skipped entry should be removed
        _middleware.History.Count.ShouldBe(2);
        _middleware.History.ShouldAllBe(e => !e.IsSkipped);
    }

    [Fact]
    public void Commit_SetsCommittedIndex()
    {
        // Arrange
        _middleware.AfterReduce(new TestAction("first"));
        _middleware.AfterReduce(new TestAction("second"));

        // Act
        _middleware.Commit();

        // Assert
        _middleware.CommittedIndex.ShouldBe(1);
    }

    [Fact]
    public void RollbackToCommitted_RestoresCommittedState()
    {
        // Arrange
        _middleware.AfterReduce(new TestAction("first"));
        _middleware.Commit();
        _middleware.AfterReduce(new TestAction("second"));
        _middleware.AfterReduce(new TestAction("third"));
        _middleware.History.Count.ShouldBe(3);

        // Act
        _middleware.RollbackToCommitted();

        // Assert - history after committed point should be removed
        _middleware.History.Count.ShouldBe(1);

        // RestoreState should have been called
        A.CallTo(() => _store.RestoreState(A<IReadOnlyDictionary<string, object>>._))
            .MustHaveHappened();
    }

    [Fact]
    public void MayDispatchAction_BlocksUserActionsDuringTimeTravel()
    {
        // Arrange - build history then jump back
        _middleware.AfterReduce(new TestAction("first"));
        int firstSeqNum = _middleware.History[0].SequenceNumber;
        _middleware.AfterReduce(new TestAction("second"));

        _middleware.JumpToAction(firstSeqNum);

        // Act & Assert - regular actions blocked
        _middleware.MayDispatchAction(new TestAction("blocked")).ShouldBeFalse();

        // HydrateSliceAction should be allowed
        _middleware.MayDispatchAction(new HydrateSliceAction("counter", 5)).ShouldBeTrue();
    }

    [Fact]
    public void AfterReduce_EnforcesMaxAgeCap()
    {
        // Arrange
        _options.MaxAge = 3;

        // Act - add more entries than MaxAge
        _middleware.AfterReduce(new TestAction("first"));
        _middleware.AfterReduce(new TestAction("second"));
        _middleware.AfterReduce(new TestAction("third"));
        _middleware.AfterReduce(new TestAction("fourth"));

        // Assert - history should be capped at MaxAge
        _middleware.History.Count.ShouldBe(3);
    }

    private sealed record TestAction(string Name);
}
