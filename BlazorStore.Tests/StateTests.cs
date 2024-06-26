using System.Collections.Immutable;
using System.Reactive.Subjects;

namespace BlazorStore.Tests;

public class StateTests
{
    private readonly ActionsSubject _actionsSubject;
    private readonly BehaviorSubject<IActionReducer<TodoState>> _reducerSubject;
    private readonly State<TodoState> _state;

    public StateTests()
    {
        _actionsSubject = new ActionsSubject();
        _reducerSubject = new BehaviorSubject<IActionReducer<TodoState>>(new TodoReducer());
        _state = new State<TodoState>(_actionsSubject, _reducerSubject, new TodoState());
    }

    [Fact]
    public async Task InitialState_Should_Be_Set_Correctly()
    {
        // Assert
        _state.Value.Todos.Should().HaveCount(3);
        _state.Value.Todos[0].Title.Should().Be("Learn Blazor");
    }

    [Fact]
    public async Task State_Should_Update_On_Action()
    {
        // Arrange
        var newTodoAction = new AddTodo("Learn Unit Testing");

        // Act
        _actionsSubject.OnNext(newTodoAction);

        // Assert
        await Task.Delay(100); // Delay to ensure async operations complete
        _state.Value.Todos.Should().HaveCount(4);
        _state.Value.Todos[3].Title.Should().Be("Learn Unit Testing");
    }

    [Fact]
    public async Task Should_Handle_Multiple_Actions()
    {
        // Arrange
        var addTodoAction = new AddTodo("Learn Testing");
        var toggleTodoAction = new ToggleTodo(_state.Value.Todos[1].Id);

        // Act
        _actionsSubject.OnNext(addTodoAction);
        _actionsSubject.OnNext(toggleTodoAction);

        // Assert
        await Task.Delay(100); // Delay to ensure async operations complete
        _state.Value.Todos.Should().HaveCount(4);
        _state.Value.Todos[1].IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void Should_Unsubscribe_On_Dispose()
    {
        // Arrange
        var newTodoAction = new AddTodo("Learn Dispose");
        var initialState = _state.Value; // Capture the state before disposal

        // Act
        _state.Dispose();
        _actionsSubject.OnNext(newTodoAction);

        // Assert
        // State should not change after dispose
        _state.Invoking(s => s.Value.Todos.Should().HaveCount(3))
            .Should().Throw<ObjectDisposedException>();
        
        // Initial state should remain the same
        initialState.Todos.Should().HaveCount(3);
    }
}


// State
public record TodoState
{
    public IImmutableList<TodoItem> Todos { get; init; } =
    [
        new TodoItem("Learn Blazor") { IsCompleted = true },
        new TodoItem("Learn Redux"),
        new TodoItem("Learn Rx.NET")
    ];
    
    // Selectors
    public IImmutableList<TodoItem> ActiveTodos
        => Todos.Where(todo => !todo.IsCompleted).ToImmutableList();
    
    public int ActiveTodosCount
        => ActiveTodos.Count;
    
    public IImmutableList<TodoItem> CompletedTodos
        => Todos.Where(todo => todo.IsCompleted).ToImmutableList();
    
    public int CompletedTodosCount
        => CompletedTodos.Count;
}

// Actions
public record AddTodo(string Title) : IAction;

public record ToggleTodo(Guid Id) : IAction;

public record RemoveTodo(Guid Id) : IAction;

// Reducer
public class TodoReducer : ActionReducer<TodoState>
{
    public TodoReducer()
    {
        Register<AddTodo>(RegisterAddTodo);
        Register<ToggleTodo>(RegisterToggleTodo);
        Register<RemoveTodo>(RegisterRemoveTodo);
    }

    private static TodoState RegisterAddTodo(TodoState state, AddTodo action)
        => state with
        {
            Todos = state.Todos.Add(new TodoItem(action.Title))
        };

    private static TodoState RegisterToggleTodo(TodoState state, ToggleTodo action)
        => state with
        {
            Todos = state.Todos
                .Select(todo => todo.Id == action.Id ? todo.ToggleIsCompleted() : todo)
                .ToImmutableList()
        };

    private static TodoState RegisterRemoveTodo(TodoState state, RemoveTodo action)
        => state with
        {
            Todos = state.Todos
                .Where(todo => todo.Id != action.Id)
                .ToImmutableList()
        };
}

public class TodoReducerFactory : IActionReducerFactory<TodoState>
{
    public IActionReducer<TodoState> CreateReducer(
        IDictionary<string, IActionReducer<TodoState>> reducers,
        TodoState initialState)
    {
        return new TodoReducer();
    }
}

public record TodoItem(string Title)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public bool IsCompleted { get; init; }
    
    public TodoItem ToggleIsCompleted()
        => this with { IsCompleted = !IsCompleted };
}