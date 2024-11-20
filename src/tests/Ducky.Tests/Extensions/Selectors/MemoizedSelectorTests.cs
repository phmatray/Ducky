// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Tests.Extensions.Selectors.Models;

namespace Ducky.Tests.Extensions.Selectors;

public class MemoizedSelectorTests
{
    [Fact]
    public void Create_ShouldCacheResult_ForSameInput()
    {
        // Arrange
        int callCount = 0;
        Func<int, int> memoizedSelector = MemoizedSelector.Create((Func<int, int>)Selector);

        // Act
        int result1 = memoizedSelector(5);
        int result2 = memoizedSelector(5);

        // Assert
        result1.Should().Be(10);
        result2.Should().Be(10);
        callCount.Should().Be(1, "because the selector function should be called only once for the same input");
        return;

        int Selector(int x)
        {
            callCount++;
            return x * 2;
        }
    }

    [Fact]
    public void Create_ShouldEvaluateSelector_ForDifferentInputs()
    {
        // Arrange
        int callCount = 0;
        Func<int, int> memoizedSelector = MemoizedSelector.Create((Func<int, int>)Selector);

        // Act
        int result1 = memoizedSelector(5);
        int result2 = memoizedSelector(10);

        // Assert
        result1.Should().Be(10);
        result2.Should().Be(20);
        callCount.Should().Be(2, "because the selector function should be called for each unique input");
        return;

        int Selector(int x)
        {
            callCount++;
            return x * 2;
        }
    }

    [Fact]
    public void Create_ShouldNotCacheResult_ForDifferentInputs()
    {
        // Arrange
        int callCount = 0;
        Func<int, int> memoizedSelector = MemoizedSelector.Create((Func<int, int>)Selector);

        // Act
        int result1 = memoizedSelector(5);
        int result2 = memoizedSelector(10);
        int result3 = memoizedSelector(5);

        // Assert
        result1.Should().Be(10);
        result2.Should().Be(20);
        result3.Should().Be(10);
        callCount.Should().Be(2, "because the selector function should be called for each unique input with cached results");
        return;

        int Selector(int x)
        {
            callCount++;
            return x * 2;
        }
    }

    [Fact]
    public void Create_ShouldCacheResult_WhenDependenciesUnchanged()
    {
        // Arrange
        int callCount = 0;
        TodoState state = new();
        Func<TodoState, int> memoizedSelector = MemoizedSelector.Create((Func<TodoState, int>)Selector, s => s.ById);

        // Act
        int result1 = memoizedSelector(state);
        int result2 = memoizedSelector(state);

        // Assert
        result1.Should().Be(0);
        result2.Should().Be(0);
        callCount.Should().Be(1, "because the selector function should be called only once when dependencies are unchanged");
        return;

        int Selector(TodoState s)
        {
            callCount++;
            return s.SelectCompletedTodos().Length;
        }
    }

    [Fact]
    public void Create_ShouldRecomputeResult_WhenDependenciesChange()
    {
        // Arrange
        int callCount = 0;
        TodoState state = new();
        state = state.SetOne(new TodoItem(Guid.NewGuid(), "Test Todo", true));
        Func<TodoState, int> memoizedSelector = MemoizedSelector.Create((Func<TodoState, int>)Selector, s => s.ById);

        // Act
        int result1 = memoizedSelector(state);
        state = state.SetOne(new TodoItem(Guid.NewGuid(), "Another Todo", true));
        int result2 = memoizedSelector(state);

        // Assert
        result1.Should().Be(1);
        result2.Should().Be(2);
        callCount.Should().Be(2, "because the selector function should be called again when dependencies change");
        return;

        int Selector(TodoState s)
        {
            callCount++;
            return s.SelectCompletedTodos().Length;
        }
    }

    [Fact]
    public void Compose_ShouldCombineSelectors()
    {
        // Arrange
        TodoState state = new();

        Func<TodoState, ImmutableArray<TodoItem>> selector1 = MemoizedSelector.Create<TodoState, ImmutableArray<TodoItem>>(
            s => s.SelectImmutableArray(todo => todo.IsCompleted),
            s => s.ById);

        Func<TodoState, int> selector2 = MemoizedSelector.Compose(
            selector1,
            todos => todos.Length,
            s => s.ById);

        // Act
        int result1 = selector2(state);

        state = state.SetOne(new TodoItem(Guid.NewGuid(), "Test Todo", true));
        int result2 = selector2(state);

        // Assert
        result1.Should().Be(0);
        result2.Should().Be(1);
    }
}
