namespace Ducky.Generator.Core;

/// <summary>
/// Options for configuring the ReducerGenerator.
/// </summary>
public class ReducerGeneratorOptions
{
    /// <summary>
    /// Gets or sets the namespace for generated reducer classes.
    /// </summary>
    public string Namespace { get; set; } = "Ducky.Reducers";

    /// <summary>
    /// Gets or sets the list of reducer descriptors to generate.
    /// </summary>
    public List<ReducerDescriptor> Reducers { get; set; } =
    [
        new ReducerDescriptor()
        {
            ReducerClassName = "TodoReducers",
            StateType = "TodoState",
            Actions = ["AddTodoAction", "ToggleTodoAction"]
        },
        new ReducerDescriptor()
        {
            ReducerClassName = "CounterReducers",
            StateType = "CounterState",
            Actions = ["IncrementAction", "DecrementAction"]
        }
    ];
}

/// <summary>
/// Describes a reducer class to be generated.
/// </summary>
public record ReducerDescriptor
{
    /// <summary>
    /// Gets the name of the reducer class to generate (e.g., "TodoReducers").
    /// </summary>
    public required string ReducerClassName { get; init; }

    /// <summary>
    /// Gets the state type this reducer operates on (e.g., "TodoState").
    /// </summary>
    public required string StateType { get; init; }

    /// <summary>
    /// Gets the list of action types this reducer handles (e.g., ["AddTodoAction", "ToggleTodoAction"]).
    /// </summary>
    public required IEnumerable<string> Actions { get; init; }

    /// <summary>
    /// Returns a string representation of the reducer descriptor.
    /// </summary>
    /// <returns>A string containing the reducer class name, state type, and action list.</returns>
    public override string ToString()
    {
        return $"{ReducerClassName} ({StateType}, {string.Join(", ", Actions)})";
    }
}
