namespace BlazorAppRxStore.Models;

public record TodoItem(string Title)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public bool IsCompleted { get; init; }
    
    public TodoItem ToggleIsCompleted()
        => this with { IsCompleted = !IsCompleted };
}