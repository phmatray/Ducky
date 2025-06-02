namespace Ducky.CodeGen.Core;

public class StateGeneratorOptions
{
    public string Namespace { get; set; } = "Ducky.States";
    
    public List<StateDescriptor> States { get; set; } = new()
    {
        new StateDescriptor
        {
            StateName = "TodoState",
            Properties = new List<PropertyDescriptor>
            {
                new() { PropertyName = "Items", PropertyType = "NormalizedState<Guid, TodoItem>", DefaultValue = "new()" },
                new() { PropertyName = "IsLoading", PropertyType = "bool", DefaultValue = "false" },
                new() { PropertyName = "Filter", PropertyType = "TodoFilter", DefaultValue = "TodoFilter.All" }
            }
        },
        new StateDescriptor
        {
            StateName = "CounterState",
            Properties = new List<PropertyDescriptor>
            {
                new() { PropertyName = "Count", PropertyType = "int", DefaultValue = "0" },
                new() { PropertyName = "Step", PropertyType = "int", DefaultValue = "1" }
            }
        }
    };
}

public record StateDescriptor
{
    public required string StateName { get; init; }
    public string? BaseClass { get; init; }
    public bool IsImmutable { get; init; } = true;
    public bool ImplementsIState { get; init; } = true;
    public IEnumerable<PropertyDescriptor> Properties { get; init; } = new List<PropertyDescriptor>();
    
    public override string ToString()
    {
        var propCount = Properties.Count();
        return $"{StateName} ({propCount} properties)";
    }
}

public record PropertyDescriptor
{
    public required string PropertyName { get; init; }
    public required string PropertyType { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public string? Summary { get; init; }
    
    public override string ToString()
    {
        return $"{PropertyType} {PropertyName}";
    }
}