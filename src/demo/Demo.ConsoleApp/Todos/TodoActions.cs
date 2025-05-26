namespace Demo.ConsoleApp.Todos;

[DuckyAction]
public sealed record AddTodo(string Title, string? Id = null);

[DuckyAction]
public sealed record ToggleTodo(string Id);

[DuckyAction]
public sealed record RemoveTodo(string Id);

[DuckyAction]
public sealed record ClearCompleted;

[DuckyAction]
public sealed record ToggleAll(bool IsCompleted);
