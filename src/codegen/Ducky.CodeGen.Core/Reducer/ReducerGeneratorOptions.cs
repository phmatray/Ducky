namespace Ducky.CodeGen.Core;

public class ReducerGeneratorOptions
{
    public string Namespace { get; set; } = "Ducky.Reducers";

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

public record ReducerDescriptor
{
    public required string ReducerClassName { get; init; } // e.g. "TodoReducers"

    public required string StateType { get; init; } // e.g. "TodoState"

    public required IEnumerable<string> Actions { get; init; } // e.g. [ "AddTodoAction", "ToggleTodoAction", … ]

    public override string ToString()
    {
        return $"{ReducerClassName} ({StateType}, {string.Join(", ", Actions)})";
    }
}
