namespace Ducky.CodeGen.Core;

public class ActionCreatorGeneratorOptions
{
    public string Namespace { get; set; } = "Ducky.Actions";

    public string StateType { get; set; } = "AppState";

    public List<ActionDescriptor> Actions { get; set; } =
    [
        new ActionDescriptor()
        {
            ActionName = "AddTodoAction",
            Parameters = [
                new ParameterDescriptor() { ParamName = "id", ParamType = "int" },
                new ParameterDescriptor() { ParamName = "title", ParamType = "string" }
            ]
        },
        new ActionDescriptor()
        {
            ActionName = "ToggleTodoAction",
            Parameters = [new ParameterDescriptor() { ParamName = "id", ParamType = "int" }]
        }
    ];
}

public record ActionDescriptor
{
    public required string ActionName { get; init; } // e.g. "AddTodoAction"

    public IEnumerable<ParameterDescriptor> Parameters { get; init; } = [];

    public override string ToString()
    {
        return $"{ActionName} ({string.Join(", ", Parameters)})";
    }
}

public record ParameterDescriptor
{
    public string ParamName { get; init; } = string.Empty;
    public string ParamType { get; init; } = "object";

    public override string ToString()
    {
        return $"{ParamType} {ParamName}";
    }
}
