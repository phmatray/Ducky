// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3dux.Tests.Extensions.Selectors.Models;

namespace R3dux.Tests.Extensions.Selectors;

public class MemoizedSelectorTests
{
    [Fact]
    public void Create_ShouldCacheResult_ForSameInput()
    {
        // Arrange
        var callCount = 0;
        var memoizedSelector = MemoizedSelector.Create((Func<int, int>)Selector);

        // Act
        var result1 = memoizedSelector(5);
        var result2 = memoizedSelector(5);

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
        var callCount = 0;
        var memoizedSelector = MemoizedSelector.Create((Func<int, int>)Selector);

        // Act
        var result1 = memoizedSelector(5);
        var result2 = memoizedSelector(10);

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
        var callCount = 0;
        var memoizedSelector = MemoizedSelector.Create((Func<int, int>)Selector);

        // Act
        var result1 = memoizedSelector(5);
        var result2 = memoizedSelector(10);
        var result3 = memoizedSelector(5);

        // Assert
        result1.Should().Be(10);
        result2.Should().Be(20);
        result3.Should().Be(10);
        callCount.Should().Be(2, "because the selector function should only be called once for each unique input, and cached results should be used");
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
        var callCount = 0;
        var state = new TodoState();
        var memoizedSelector = MemoizedSelector.Create((Func<TodoState, int>)Selector, s => s.ById);

        // Act
        var result1 = memoizedSelector(state);
        var result2 = memoizedSelector(state);

        // Assert
        result1.Should().Be(0);
        result2.Should().Be(0);
        callCount.Should().Be(1, "because the selector function should be called only once when dependencies are unchanged");
        return;

        int Selector(TodoState s)
        {
            callCount++;
            return s.SelectCompletedTodos().Count;
        }
    }

    [Fact]
    public void Create_ShouldRecomputeResult_WhenDependenciesChange()
    {
        // Arrange
        var callCount = 0;
        var state = new TodoState();
        state = state.SetOne(new TodoItem(Guid.NewGuid(), "Test Todo", true));
        var memoizedSelector = MemoizedSelector.Create((Func<TodoState, int>)Selector, s => s.ById);

        // Act
        var result1 = memoizedSelector(state);
        state = state.SetOne(new TodoItem(Guid.NewGuid(), "Another Todo", true));
        var result2 = memoizedSelector(state);

        // Assert
        result1.Should().Be(1);
        result2.Should().Be(2);
        callCount.Should().Be(2, "because the selector function should be called again when dependencies change");
        return;

        int Selector(TodoState s)
        {
            callCount++;
            return s.SelectCompletedTodos().Count;
        }
    }

    [Fact]
    public void Compose_ShouldCombineSelectors()
    {
        // Arrange
        var state = new TodoState();

        var selector1 = MemoizedSelector.Create<TodoState, ImmutableList<TodoItem>>(
            s => s.SelectImmutableList(todo => todo.IsCompleted),
            s => s.ById);

        var selector2 = MemoizedSelector.Compose(
            selector1,
            todos => todos.Count,
            s => s.ById);

        // Act
        var result1 = selector2(state);

        state = state.SetOne(new TodoItem(Guid.NewGuid(), "Test Todo", true));
        var result2 = selector2(state);

        // Assert
        result1.Should().Be(0);
        result2.Should().Be(1);
    }
}
