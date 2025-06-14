namespace Ducky.Generator.Core;

/// <summary>
/// Options for configuring the StateGenerator.
/// </summary>
public class StateGeneratorOptions
{
    /// <summary>
    /// Gets or sets the namespace for generated state classes.
    /// </summary>
    public string Namespace { get; set; } = "Ducky.States";

    /// <summary>
    /// Gets or sets the list of state descriptors to generate.
    /// </summary>
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

/// <summary>
/// Describes a state class to be generated.
/// </summary>
public record StateDescriptor
{
    /// <summary>
    /// Gets the name of the state class.
    /// </summary>
    public required string StateName { get; init; }
    /// <summary>
    /// Gets an optional base class for the state.
    /// </summary>
    public string? BaseClass { get; init; }
    /// <summary>
    /// Gets a value indicating whether the state is immutable.
    /// </summary>
    public bool IsImmutable { get; init; } = true;
    /// <summary>
    /// Gets a value indicating whether the state implements IState interface.
    /// </summary>
    public bool ImplementsIState { get; init; } = true;
    
    /// <summary>
    /// Gets the list of properties for this state.
    /// </summary>
    public IEnumerable<PropertyDescriptor> Properties { get; init; } = new List<PropertyDescriptor>();
    
    /// <summary>
    /// Returns a string representation of the state descriptor.
    /// </summary>
    /// <returns>A string containing the state name and property count.</returns>
    public override string ToString()
    {
        var propCount = Properties.Count();
        return $"{StateName} ({propCount.ToString()} properties)";
    }
}

/// <summary>
/// Describes a property within a state class.
/// </summary>
public record PropertyDescriptor
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public required string PropertyName { get; init; }
    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    public required string PropertyType { get; init; }
    /// <summary>
    /// Gets an optional default value for the property.
    /// </summary>
    public string? DefaultValue { get; init; }
    /// <summary>
    /// Gets a value indicating whether the property is read-only.
    /// </summary>
    public bool IsReadOnly { get; init; } = true;
    /// <summary>
    /// Gets an optional summary description for the property.
    /// </summary>
    public string? Summary { get; init; }
    
    /// <summary>
    /// Returns a string representation of the property descriptor.
    /// </summary>
    /// <returns>A string containing the property type and name.</returns>
    public override string ToString()
    {
        return $"{PropertyType} {PropertyName}";
    }
}