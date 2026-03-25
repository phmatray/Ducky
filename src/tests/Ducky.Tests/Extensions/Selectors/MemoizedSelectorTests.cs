// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
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
        result1.ShouldBe(10);
        result2.ShouldBe(10);
        callCount.ShouldBe(1, "because the selector function should be called only once for the same input");
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
        result1.ShouldBe(10);
        result2.ShouldBe(20);
        callCount.ShouldBe(2, "because the selector function should be called for each unique input");
        return;

        int Selector(int x)
        {
            callCount++;
            return x * 2;
        }
    }

    [Fact]
    public void Create_ShouldRecompute_WhenInputChangesBackToPrevious()
    {
        // Arrange
        int callCount = 0;
        Func<int, int> memoizedSelector = MemoizedSelector.Create((Func<int, int>)Selector);

        // Act
        int result1 = memoizedSelector(5);
        int result2 = memoizedSelector(10);
        int result3 = memoizedSelector(5);

        // Assert
        result1.ShouldBe(10);
        result2.ShouldBe(20);
        result3.ShouldBe(10);
        callCount.ShouldBe(3, "because the single-entry cache only retains the last input, so reverting to a previous input recomputes");
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
        result1.ShouldBe(0);
        result2.ShouldBe(0);
        callCount.ShouldBe(1, "because the selector function should be called only once when dependencies are unchanged");
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
        result1.ShouldBe(1);
        result2.ShouldBe(2);
        callCount.ShouldBe(2, "because the selector function should be called again when dependencies change");
        return;

        int Selector(TodoState s)
        {
            callCount++;
            return s.SelectCompletedTodos().Length;
        }
    }

    [Fact]
    public void Create_ShouldOnlyCacheLastState()
    {
        // Arrange - verify single-entry cache by checking that only the most recent
        // state produces a cache hit (callCount doesn't increase)
        int callCount = 0;
        Func<int, int> memoizedSelector = MemoizedSelector.Create((Func<int, int>)Selector);

        // Act - call with 100 different inputs, then repeat the last one
        for (int i = 0; i < 100; i++)
        {
            memoizedSelector(i);
        }

        int countBeforeRepeat = callCount;
        memoizedSelector(99); // repeat last input - should be cached

        // Assert
        callCount.ShouldBe(countBeforeRepeat, "because the last input should still be cached");
        callCount.ShouldBe(100, "because each unique input should have been computed exactly once");
        return;

        int Selector(int x)
        {
            callCount++;
            return x * 2;
        }
    }

    [Fact]
    public void Create_ShouldNotRetainAllPreviousStates()
    {
        // Arrange - verify the cache is bounded by checking that earlier inputs
        // are NOT cached (they must be recomputed)
        int callCount = 0;
        Func<int, int> memoizedSelector = MemoizedSelector.Create((Func<int, int>)Selector);

        // Act - call with inputs 0..9, then call with input 0 again
        for (int i = 0; i < 10; i++)
        {
            memoizedSelector(i);
        }

        int countBefore = callCount; // should be 10
        memoizedSelector(0); // input 0 was evicted, must recompute

        // Assert
        callCount.ShouldBe(countBefore + 1, "because earlier inputs should be evicted from the single-entry cache");
        return;

        int Selector(int x)
        {
            callCount++;
            return x * 2;
        }
    }

    [Fact]
    public void Compose_ShouldCombineSelectors()
    {
        // Arrange
        TodoState state = new();

        Func<TodoState, ImmutableArray<TodoItem>> selector1 =
            MemoizedSelector.Create<TodoState, ImmutableArray<TodoItem>>(
                s => s.SelectEntities(todo => todo.IsCompleted),
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
        result1.ShouldBe(0);
        result2.ShouldBe(1);
    }
}
