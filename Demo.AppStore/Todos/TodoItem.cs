namespace Demo.AppStore;

public record TodoItem(string Title, bool IsCompleted = false)
{
    public TodoItem(Guid id, string title, bool isCompleted = false)
        : this(title, isCompleted)
        => Id = id;

    public Guid Id { get; init; } = Guid.NewGuid();
    
    public TodoItem ToggleIsCompleted()
        => this with { IsCompleted = !IsCompleted };
}

public static class SampleIds
{
    public static Guid Id1 { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static Guid Id2 { get; } = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static Guid Id3 { get; } = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static Guid Id4 { get; } = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public static Guid Id5 { get; } = Guid.Parse("00000000-0000-0000-0000-000000000005");
}