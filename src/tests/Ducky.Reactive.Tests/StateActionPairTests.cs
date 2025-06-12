// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Tests;

public class StateActionPairTests
{
    [Fact]
    public void StateActionPair_ShouldInitializeCorrectly()
    {
        // Arrange
        TestState state = new("test-state", 42);
        TestAction action = new("test-action");

        // Act
        StateActionPair<TestState, TestAction> pair = new(state, action);

        // Assert
        pair.State.ShouldBe(state);
        pair.Action.ShouldBe(action);
    }

    [Fact]
    public void StateActionPair_WithNullState_ShouldAllowNull()
    {
        // Arrange
        TestState? state = null;
        TestAction action = new("test-action");

        // Act
        StateActionPair<TestState?, TestAction> pair = new(state, action);

        // Assert
        pair.State.ShouldBeNull();
        pair.Action.ShouldBe(action);
    }

    [Fact]
    public void StateActionPair_WithNullAction_ShouldAllowNull()
    {
        // Arrange
        TestState state = new("test-state", 42);
        TestAction? action = null;

        // Act
        StateActionPair<TestState, TestAction?> pair = new(state, action);

        // Assert
        pair.State.ShouldBe(state);
        pair.Action.ShouldBeNull();
    }

    [Fact]
    public void StateActionPair_Equality_ShouldCompareByValue()
    {
        // Arrange
        TestState state1 = new("state", 100);
        TestState state2 = new("state", 100);
        TestAction action1 = new("action");
        TestAction action2 = new("action");

        StateActionPair<TestState, TestAction> pair1 = new(state1, action1);
        StateActionPair<TestState, TestAction> pair2 = new(state2, action2);
        StateActionPair<TestState, TestAction> pair3 = new(state1, new TestAction("different"));

        // Act & Assert
        pair1.ShouldBe(pair2);
        pair1.ShouldNotBe(pair3);
        (pair1 == pair2).ShouldBeTrue();
        (pair1 != pair3).ShouldBeTrue();
    }

    [Fact]
    public void StateActionPair_ToString_ShouldIncludeBothValues()
    {
        // Arrange
        TestState state = new("my-state", 123);
        TestAction action = new("my-action");
        StateActionPair<TestState, TestAction> pair = new(state, action);

        // Act
        string stringRepresentation = pair.ToString();

        // Assert
        stringRepresentation.ShouldContain("my-state");
        stringRepresentation.ShouldContain("123");
        stringRepresentation.ShouldContain("my-action");
    }

    [Fact]
    public void StateActionPair_GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        TestState state = new("state", 42);
        TestAction action = new("action");

        StateActionPair<TestState, TestAction> pair1 = new(state, action);
        StateActionPair<TestState, TestAction> pair2 = new(state, action);

        // Act
        int hash1 = pair1.GetHashCode();
        int hash2 = pair2.GetHashCode();

        // Assert
        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void StateActionPair_Deconstruction_ShouldWork()
    {
        // Arrange
        TestState expectedState = new("state", 999);
        TestAction expectedAction = new("action");
        StateActionPair<TestState, TestAction> pair = new(expectedState, expectedAction);

        // Act
        (TestState state, TestAction action) = pair;

        // Assert
        state.ShouldBe(expectedState);
        action.ShouldBe(expectedAction);
    }

    [Fact]
    public void StateActionPair_WithComplexTypes_ShouldWork()
    {
        // Arrange
        ComplexState state = new()
        {
            Items = ["item1", "item2"],
            Metadata = new Dictionary<string, object> { ["key"] = "value" }
        };
        ComplexAction action = new()
        {
            Payload = new { Id = 1, Name = "Test" },
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        StateActionPair<ComplexState, ComplexAction> pair = new(state, action);

        // Assert
        pair.State.ShouldBe(state);
        pair.Action.ShouldBe(action);
        pair.State.Items.ShouldBe(["item1", "item2"]);
        pair.Action.Payload.ShouldNotBeNull();
    }

    // Test helper types
    private record TestState(string Name, int Value);

    private record TestAction(string Type);

    private record ComplexState
    {
        public required List<string> Items { get; init; }
        public required Dictionary<string, object> Metadata { get; init; }
    }

    private record ComplexAction
    {
        public required object Payload { get; init; }
        public DateTimeOffset Timestamp { get; init; }
    }
}