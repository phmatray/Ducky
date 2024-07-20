using System.Collections.Immutable;
using R3dux.Normalization;

namespace R3dux.Tests.Extensions.Selectors;

public class MemoizedSelectorTests
{
    [Fact]
    public void Create_ShouldCacheResult_ForSameInput()
    {
        // Arrange
        int callCount = 0;
        Func<int, int> selector = x =>
        {
            callCount++;
            return x * 2;
        };
        var memoizedSelector = MemoizedSelector.Create(selector);

        // Act
        var result1 = memoizedSelector(5);
        var result2 = memoizedSelector(5);

        // Assert
        result1.Should().Be(10);
        result2.Should().Be(10);
        callCount.Should().Be(1, "because the selector function should be called only once for the same input");
    }

    [Fact]
    public void Create_ShouldEvaluateSelector_ForDifferentInputs()
    {
        // Arrange
        int callCount = 0;
        Func<int, int> selector = x =>
        {
            callCount++;
            return x * 2;
        };
        var memoizedSelector = MemoizedSelector.Create(selector);

        // Act
        var result1 = memoizedSelector(5);
        var result2 = memoizedSelector(10);

        // Assert
        result1.Should().Be(10);
        result2.Should().Be(20);
        callCount.Should().Be(2, "because the selector function should be called for each unique input");
    }

    [Fact]
    public void Create_ShouldNotCacheResult_ForDifferentInputs()
    {
        // Arrange
        int callCount = 0;
        Func<int, int> selector = x =>
        {
            callCount++;
            return x * 2;
        };
        var memoizedSelector = MemoizedSelector.Create(selector);

        // Act
        var result1 = memoizedSelector(5);
        var result2 = memoizedSelector(10);
        var result3 = memoizedSelector(5);

        // Assert
        result1.Should().Be(10);
        result2.Should().Be(20);
        result3.Should().Be(10);
        callCount.Should().Be(2, "because the selector function should only be called once for each unique input, and cached results should be used");
    }
    
    [Fact]
    public void Create_ShouldCacheResult_WhenDependenciesUnchanged()
    {
        // Arrange
        int callCount = 0;
        var state = new TodoState();
        Func<TodoState, int> selector = s =>
        {
            callCount++;
            return s.SelectCompletedTodos().Count;
        };
        var memoizedSelector = MemoizedSelector.Create(selector, s => s.ById);

        // Act
        var result1 = memoizedSelector(state);
        var result2 = memoizedSelector(state);

        // Assert
        result1.Should().Be(0);
        result2.Should().Be(0);
        callCount.Should().Be(1, "because the selector function should be called only once when dependencies are unchanged");
    }

    [Fact]
    public void Create_ShouldRecomputeResult_WhenDependenciesChange()
    {
        // Arrange
        int callCount = 0;
        var state = new TodoState();
        state = state.SetOne(new TodoItem(Guid.NewGuid(), "Test Todo", true));
        Func<TodoState, int> selector = s =>
        {
            callCount++;
            return s.SelectCompletedTodos().Count;
        };
        var memoizedSelector = MemoizedSelector.Create(selector, s => s.ById);

        // Act
        var result1 = memoizedSelector(state);
        state = state.SetOne(new TodoItem(Guid.NewGuid(), "Another Todo", true));
        var result2 = memoizedSelector(state);

        // Assert
        result1.Should().Be(1);
        result2.Should().Be(2);
        callCount.Should().Be(2, "because the selector function should be called again when dependencies change");
    }

    [Fact]
    public void Compose_ShouldCombineSelectors()
    {
        // Arrange
        var state = new TodoState();
        var selector1 = MemoizedSelector.Create<TodoState, ImmutableList<TodoItem>>(
            s => s.SelectImmutableList(todo => todo.IsCompleted),
            s => s.ById
        );
        var selector2 = MemoizedSelector.Compose(
            selector1,
            todos => todos.Count,
            s => s.ById
        );

        // Act
        var result1 = selector2(state);

        state = state.SetOne(new TodoItem(Guid.NewGuid(), "Test Todo", true));
        var result2 = selector2(state);

        // Assert
        result1.Should().Be(0);
        result2.Should().Be(1);
    }
}

file record TodoState : NormalizedState<Guid, TodoItem, TodoState>
{
    private readonly Func<TodoState, ImmutableList<TodoItem>> _selectCompletedTodos;
    private readonly Func<TodoState, int> _selectCompletedTodosCount;
    private readonly Func<TodoState, bool> _selectHasCompletedTodos;

    private readonly Func<TodoState, ImmutableList<TodoItem>> _selectActiveTodos;
    private readonly Func<TodoState, int> _selectActiveTodosCount;
    private readonly Func<TodoState, bool> _selectHasActiveTodos;

    public TodoState()
    {
        _selectCompletedTodos = MemoizedSelector.Create<TodoState, ImmutableList<TodoItem>>(
            state => state.SelectImmutableList(todo => todo.IsCompleted),
            state => state.ById
        );

        _selectCompletedTodosCount = MemoizedSelector.Compose(
            _selectCompletedTodos,
            todos => todos.Count,
            state => state.ById
        );

        _selectHasCompletedTodos = MemoizedSelector.Compose(
            _selectCompletedTodos,
            todos => !todos.IsEmpty,
            state => state.ById
        );

        _selectActiveTodos = MemoizedSelector.Create<TodoState, ImmutableList<TodoItem>>(
            state => state.SelectImmutableList(todo => !todo.IsCompleted),
            state => state.ById
        );

        _selectActiveTodosCount = MemoizedSelector.Compose(
            _selectActiveTodos,
            todos => todos.Count,
            state => state.ById
        );

        _selectHasActiveTodos = MemoizedSelector.Compose(
            _selectActiveTodos,
            todos => !todos.IsEmpty,
            state => state.ById
        );
    }

    // Memoized Selectors
    public ImmutableList<TodoItem> SelectCompletedTodos()
        => _selectCompletedTodos(this);

    public int SelectCompletedTodosCount()
        => _selectCompletedTodosCount(this);

    public bool SelectHasCompletedTodos()
        => _selectHasCompletedTodos(this);

    public ImmutableList<TodoItem> SelectActiveTodos()
        => _selectActiveTodos(this);

    public int SelectActiveTodosCount()
        => _selectActiveTodosCount(this);

    public bool SelectHasActiveTodos()
        => _selectHasActiveTodos(this);
}

file record TodoItem(Guid Id, string Title, bool IsCompleted = false) : IEntity<Guid>;