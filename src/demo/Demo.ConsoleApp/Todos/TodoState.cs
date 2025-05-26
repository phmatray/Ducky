namespace Demo.ConsoleApp.Todos;

public sealed record TodoItem(string Id, string Title, bool IsCompleted = false) : IEntity<string>
{
    public string EntityId => Id;
}

public sealed record TodoState : NormalizedState<string, TodoItem, TodoState>
{
    public int CompletedCount => SelectEntities(t => t.IsCompleted).Count;
    public int ActiveCount => SelectEntities(t => !t.IsCompleted).Count;
}
