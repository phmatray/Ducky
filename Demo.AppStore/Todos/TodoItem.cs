namespace Demo.AppStore;

public class TodoItem : IEntity<Guid>
{
    public TodoItem(Guid id, string title, bool isCompleted = false)
    {
        Id = id;
        Title = title;
        IsCompleted = isCompleted;
    }

    public TodoItem(string title, bool isCompleted = false)
        : this(Guid.NewGuid(), title, isCompleted)
    {
    }

    public Guid Id { get; }
    public string Title { get; set; }
    public bool IsCompleted { get; set; }
}