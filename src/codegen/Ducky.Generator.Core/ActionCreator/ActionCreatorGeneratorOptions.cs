namespace Ducky.Generator.Core;

/// <summary>
/// Configuration options for generating action creator classes.
/// </summary>
public class ActionCreatorGeneratorOptions
{
    /// <summary>
    /// Gets or sets the namespace for the generated action creators.
    /// </summary>
    public string Namespace { get; set; } = "Ducky.Actions";

    /// <summary>
    /// Gets or sets the state type that the actions will operate on.
    /// </summary>
    public string StateType { get; set; } = "AppState";

    /// <summary>
    /// Gets or sets the list of actions to generate creators for.
    /// </summary>
    public List<ActionDescriptor> Actions { get; set; } = new List<ActionDescriptor>
    {
        new ActionDescriptor()
        {
            ActionName = "AddTodoAction",
            Parameters = new List<ParameterDescriptor>
            {
                new ParameterDescriptor() { ParamName = "id", ParamType = "int" },
                new ParameterDescriptor() { ParamName = "title", ParamType = "string" }
            }
        },
        new ActionDescriptor()
        {
            ActionName = "ToggleTodoAction",
            Parameters = new List<ParameterDescriptor> { new ParameterDescriptor() { ParamName = "id", ParamType = "int" } }
        }
    };
}

/// <summary>
/// Describes an action for code generation.
/// </summary>
public record ActionDescriptor
{
    /// <summary>
    /// Gets the name of the action (e.g., "AddTodoAction").
    /// </summary>
    public string ActionName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parameters for the action.
    /// </summary>
    public IEnumerable<ParameterDescriptor> Parameters { get; init; } = new List<ParameterDescriptor>();

    /// <summary>
    /// Returns a string representation of the action descriptor.
    /// </summary>
    /// <returns>A string in the format "ActionName (param1, param2, ...)".</returns>
    public override string ToString()
    {
        return $"{ActionName} ({string.Join(", ", Parameters)})";
    }
}

/// <summary>
/// Describes a parameter for an action.
/// </summary>
public record ParameterDescriptor
{
    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public string ParamName { get; init; } = string.Empty;
    
    /// <summary>
    /// Gets the type of the parameter.
    /// </summary>
    public string ParamType { get; init; } = "object";

    /// <summary>
    /// Returns a string representation of the parameter descriptor.
    /// </summary>
    /// <returns>A string in the format "Type Name".</returns>
    public override string ToString()
    {
        return $"{ParamType} {ParamName}";
    }
}
