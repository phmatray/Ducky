// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Tests;

public class CustomOperatorsTests
{
    [Fact]
    public void OfActionType_WithMatchingType_ShouldReturnTypedActions()
    {
        // Arrange
        Subject<object> source = new();
        List<TestAction> results = [];

        // Act
        source.OfActionType<TestAction>().Subscribe(results.Add);

        source.OnNext(new TestAction("test1"));
        source.OnNext(new OtherAction("other"));
        source.OnNext(new TestAction("test2"));
        source.OnCompleted();

        // Assert
        results.Count.ShouldBe(2);
        results[0].Message.ShouldBe("test1");
        results[1].Message.ShouldBe("test2");
    }

    [Fact]
    public void WithSliceState_ShouldCombineActionAndState()
    {
        // Arrange
        Subject<TestAction> actionSubject = new();
        Subject<IRootState> stateSubject = new();
        List<StateActionPair<TestState, TestAction>> results = [];

        TestState testState = new("test-state");
        Mock<IRootState> rootStateMock = new();
        rootStateMock.Setup(x => x.GetSliceState<TestState>()).Returns(testState);

        // Act
        actionSubject
            .WithSliceState<TestState, TestAction>(stateSubject)
            .Subscribe(results.Add);

        stateSubject.OnNext(rootStateMock.Object);
        actionSubject.OnNext(new TestAction("action1"));
        actionSubject.OnNext(new TestAction("action2"));

        // Assert
        results.Count.ShouldBe(2);
        results[0].State.ShouldBe(testState);
        results[0].Action.Message.ShouldBe("action1");
        results[1].State.ShouldBe(testState);
        results[1].Action.Message.ShouldBe("action2");
    }

    [Fact]
    public void WithSliceState_WithKey_ShouldUseSpecificSliceKey()
    {
        // Arrange
        Subject<TestAction> actionSubject = new();
        Subject<IRootState> stateSubject = new();
        List<StateActionPair<TestState, TestAction>> results = [];

        TestState testState = new("keyed-state");
        Mock<IRootState> rootStateMock = new();
        rootStateMock.Setup(x => x.GetSliceState<TestState>("customKey")).Returns(testState);

        // Act
        actionSubject
            .WithSliceState<TestState, TestAction>(stateSubject, "customKey")
            .Subscribe(results.Add);

        stateSubject.OnNext(rootStateMock.Object);
        actionSubject.OnNext(new TestAction("action1"));

        // Assert
        results.Count.ShouldBe(1);
        results[0].State.ShouldBe(testState);
        results[0].Action.Message.ShouldBe("action1");
        rootStateMock.Verify(x => x.GetSliceState<TestState>("customKey"), Times.Once);
    }

    [Fact]
    public void SelectAction_ShouldTransformAndCastToObject()
    {
        // Arrange
        Subject<int> source = new();
        List<object> results = [];

        // Act
        source.SelectAction(x => new TestAction($"number-{x}")).Subscribe(results.Add);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);

        // Assert
        results.Count.ShouldBe(3);
        results[0].ShouldBeOfType<TestAction>();
        ((TestAction)results[0]).Message.ShouldBe("number-1");
        results[1].ShouldBeOfType<TestAction>();
        ((TestAction)results[1]).Message.ShouldBe("number-2");
        results[2].ShouldBeOfType<TestAction>();
        ((TestAction)results[2]).Message.ShouldBe("number-3");
    }

    [Fact]
    public void CatchAction_ShouldHandleExceptionsAndReturnReplacementValue()
    {
        // Arrange
        Subject<string> source = new();
        List<string> results = [];

        // Act
        source
            .Select(x => x.Length > 5 ? x : throw new InvalidOperationException("Too short"))
            .CatchAction(ex => "Error handled")
            .Subscribe(results.Add);

        source.OnNext("hello world");
        source.OnNext("hi"); // This will throw and complete the sequence
        source.OnNext("another long string"); // This won't be processed

        // Assert
        results.Count.ShouldBe(2);
        results[0].ShouldBe("hello world");
        results[1].ShouldBe("Error handled"); // The error gets replaced and sequence completes
    }

    [Fact]
    public async Task InvokeService_WithSuccessfulCall_ShouldReturnSuccessAction()
    {
        // Arrange
        Subject<TestAction> source = new();
        List<object> results = [];

        // Act
        source
            .InvokeService(
                action => ValueTask.FromResult($"Result: {action.Message}"),
                result => new { Type = "SUCCESS", Result = result },
                error => new { Type = "ERROR", Message = error.Message })
            .Subscribe(results.Add);

        source.OnNext(new TestAction("test"));

        // Wait for async operation
        await Task.Delay(10, TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        dynamic result = results[0];
        ((string)result.Type).ShouldBe("SUCCESS");
        ((string)result.Result).ShouldBe("Result: test");
    }

    [Fact]
    public async Task InvokeService_WithFailingCall_ShouldReturnErrorAction()
    {
        // Arrange
        Subject<TestAction> source = new();
        List<object> results = [];

        // Act
        source
            .InvokeService(
                action => ValueTask.FromException<string>(new InvalidOperationException("Service failed")),
                result => new { Type = "SUCCESS", Result = result },
                error => new { Type = "ERROR", Message = error.Message })
            .Subscribe(results.Add);

        source.OnNext(new TestAction("test"));

        // Wait for async operation
        await Task.Delay(10, TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        dynamic result = results[0];
        ((string)result.Type).ShouldBe("ERROR");
        ((string)result.Message).ShouldBe("Service failed");
    }

    [Fact]
    public void LogMessage_ShouldPassThroughValuesAndLogMessage()
    {
        // Arrange
        Subject<int> source = new();
        List<int> results = [];

        // Act
        source
            .LogMessage("Test message")
            .Subscribe(results.Add);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);

        // Assert
        results.Count.ShouldBe(3);
        results.ShouldBe([1, 2, 3]);
        // Note: In a real test, you might want to capture console output to verify logging
    }

    // Test helper classes
    public record TestAction(string Message);

    public record OtherAction(string Message);

    public record TestState(string Value);
}