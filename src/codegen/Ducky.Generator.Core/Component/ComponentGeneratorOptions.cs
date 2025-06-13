namespace Ducky.Generator.Core;

public class ComponentGeneratorOptions
{
    /// <summary>
    /// The target namespace for generated components.
    /// </summary>
    public string Namespace { get; set; } = "MyApp.Components";

    /// <summary>
    /// The root state type (e.g., "AppState").
    /// </summary>
    public string RootStateType { get; set; } = "AppState";

    /// <summary>
    /// List of component configurations to generate.
    /// </summary>
    public List<ComponentDescriptor> Components { get; set; } = new List<ComponentDescriptor>
    {
        new ComponentDescriptor
        {
            ComponentName = "TodoStateComponent",
            StateSliceName = "Todos",
            StateSliceType = "TodoState",
            StateSliceProperty = "Todos",
            Actions = new List<ComponentActionDescriptor>
            {
                new ComponentActionDescriptor 
                { 
                    ActionName = "AddTodo", 
                    ActionType = "AddTodoAction", 
                    Parameters = new List<ParameterDescriptor>
                    {
                        new ParameterDescriptor { ParamName = "id", ParamType = "int" },
                        new ParameterDescriptor { ParamName = "title", ParamType = "string" }
                    }
                },
                new ComponentActionDescriptor 
                { 
                    ActionName = "ToggleTodo", 
                    ActionType = "ToggleTodoAction", 
                    Parameters = new List<ParameterDescriptor>
                    {
                        new ParameterDescriptor { ParamName = "id", ParamType = "int" }
                    }
                },
                new ComponentActionDescriptor 
                { 
                    ActionName = "RemoveTodo", 
                    ActionType = "RemoveTodoAction", 
                    Parameters = new List<ParameterDescriptor>
                    {
                        new ParameterDescriptor { ParamName = "id", ParamType = "int" }
                    }
                },
                new ComponentActionDescriptor 
                { 
                    ActionName = "FetchTodos", 
                    ActionType = "FetchTodosAction", 
                    Parameters = new List<ParameterDescriptor>()
                },
                new ComponentActionDescriptor 
                { 
                    ActionName = "FetchTodosSuccess", 
                    ActionType = "FetchTodosSuccessAction", 
                    Parameters = new List<ParameterDescriptor>
                    {
                        new ParameterDescriptor { ParamName = "items", ParamType = "IEnumerable<TodoItem>" }
                    }
                },
                new ComponentActionDescriptor 
                { 
                    ActionName = "FetchTodosError", 
                    ActionType = "FetchTodosErrorAction", 
                    Parameters = new List<ParameterDescriptor>
                    {
                        new ParameterDescriptor { ParamName = "error", ParamType = "string" }
                    }
                }
            }
        },
        new ComponentDescriptor
        {
            ComponentName = "NotificationStateComponent",
            StateSliceName = "Notifications",
            StateSliceType = "NotificationState",
            StateSliceProperty = "Notifications",
            Actions = new List<ComponentActionDescriptor>
            {
                new ComponentActionDescriptor 
                { 
                    ActionName = "AddNotification", 
                    ActionType = "AddNotificationAction", 
                    Parameters = new List<ParameterDescriptor>
                    {
                        new ParameterDescriptor { ParamName = "id", ParamType = "int" },
                        new ParameterDescriptor { ParamName = "message", ParamType = "string" }
                    }
                },
                new ComponentActionDescriptor 
                { 
                    ActionName = "MarkNotificationRead", 
                    ActionType = "MarkNotificationReadAction", 
                    Parameters = new List<ParameterDescriptor>
                    {
                        new ParameterDescriptor { ParamName = "id", ParamType = "int" }
                    }
                }
            }
        }
    };
}

/// <summary>
/// Describes a component to generate.
/// </summary>
public record ComponentDescriptor
{
    /// <summary>
    /// The name of the component class (e.g., "TodoStateComponent").
    /// </summary>
    public string ComponentName { get; init; } = string.Empty;

    /// <summary>
    /// The name of the state slice for documentation (e.g., "Todos").
    /// </summary>
    public string StateSliceName { get; init; } = string.Empty;

    /// <summary>
    /// The type of the state slice (e.g., "TodoState").
    /// </summary>
    public string StateSliceType { get; init; } = string.Empty;

    /// <summary>
    /// The property path to access the state slice (e.g., "Todos").
    /// </summary>
    public string StateSliceProperty { get; init; } = string.Empty;

    /// <summary>
    /// List of action methods to generate for this component.
    /// </summary>
    public IEnumerable<ComponentActionDescriptor> Actions { get; init; } = new List<ComponentActionDescriptor>();

    public override string ToString()
    {
        var actionCount = Actions.Count();
        return $"{ComponentName} ({actionCount.ToString()} actions)";
    }
}

/// <summary>
/// Describes an action method to generate in a component.
/// </summary>
public record ComponentActionDescriptor
{
    /// <summary>
    /// The method name (e.g., "AddTodo").
    /// </summary>
    public string ActionName { get; init; } = string.Empty;

    /// <summary>
    /// The action type to dispatch (e.g., "AddTodoAction").
    /// </summary>
    public string ActionType { get; init; } = string.Empty;

    /// <summary>
    /// Parameters for the action method.
    /// </summary>
    public IEnumerable<ParameterDescriptor> Parameters { get; init; } = new List<ParameterDescriptor>();

    public override string ToString()
    {
        var paramCount = Parameters.Count();
        return $"{ActionName} -> {ActionType} ({paramCount.ToString()} params)";
    }
}