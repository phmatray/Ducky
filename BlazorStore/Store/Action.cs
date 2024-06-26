namespace BlazorStore;

public class Action : IAction
{
    public required string Type { get; set; }
    public required object? Payload { get; set; }
}