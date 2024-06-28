namespace Demo.AppStore;

public record TodoItem(string Title, bool IsCompleted = false)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public TodoItem ToggleIsCompleted()
        => this with { IsCompleted = !IsCompleted };
}