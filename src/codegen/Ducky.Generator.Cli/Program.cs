using Ducky.Generator.Core;
using Spectre.Console;

namespace Ducky.Generator.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("CodeGen")
                .Color(Color.Green));

        // 1) Let user pick a generator
        (string Name, Func<Task>)[] generators =
        {
            ("Action Creator", RunActionCreatorAsync),
            ("Action Dispatcher", RunActionDispatcherAsync),
            ("Component", RunComponentAsync),
            ("State", RunStateAsync),
            ("Reducer", RunReducerAsync),
            ("Effects", RunEffectsAsync)
        };
        string choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a [green]generator[/]:")
                .AddChoices(generators.Select(g => g.Name)));

        // 2) Invoke the chosen generator
        await generators.First(g => g.Name == choice).Item2().ConfigureAwait(false);

        return 0;
    }

    private static async Task RunActionCreatorAsync()
    {
        // Prompt for options
        string @namespace = AnsiConsole.Ask<string>("Namespace?");
        string className = AnsiConsole.Ask<string>("Class name?");
        string stateType = AnsiConsole.Ask<string>("State type?");
        string actionsCsv = AnsiConsole.Ask<string>("Actions (Name:Payload, …)?");

        // Parse into strongly-typed opts
        List<(string Name, string PayloadType)> actions = actionsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair =>
            {
                string[] parts = pair.Split(':', 2);
                return (Name: parts[0].Trim(), PayloadType: parts[1].Trim());
            })
            .ToList();

        ActionCreatorGeneratorOptions opts = new()
        {
            Namespace = @namespace,
            StateType = stateType,
            Actions = actions
                .Select(a => new ActionDescriptor
                {
                    ActionName = className + a.Name,
                    Parameters = new List<ParameterDescriptor>
                    {
                        new()
                        {
                            ParamName = a.Name,
                            ParamType = a.PayloadType
                        }
                    }
                })
                .ToList()
        };

        // Generate
        ActionCreatorGenerator generator = new();
        string code = await generator.GenerateCodeAsync(opts).ConfigureAwait(false);

        // Show the result
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(code)
                .Header("Generated Code")
                .Expand());
    }

    private static async Task RunActionDispatcherAsync()
    {
        AnsiConsole.MarkupLine("[red]ActionDispatcher generator is not yet implemented[/]");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async Task RunComponentAsync()
    {
        string @namespace = AnsiConsole.Ask<string>("Namespace?");
        string rootStateType = AnsiConsole.Ask<string>("Root state type (e.g., AppState)?");
        string componentName = AnsiConsole.Ask<string>("Component name (e.g., TodoStateComponent)?");
        string stateSliceName = AnsiConsole.Ask<string>("State slice name (e.g., Todos)?");
        string stateSliceType = AnsiConsole.Ask<string>("State slice type (e.g., TodoState)?");
        string actionsCsv = AnsiConsole.Ask<string>("Actions (name:type:params, ...)?");

        List<ComponentActionDescriptor> actions = [];
        if (!string.IsNullOrWhiteSpace(actionsCsv))
        {
            actions = actionsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(actionSpec =>
                {
                    string[] parts = actionSpec.Split(':', 3);
                    List<ParameterDescriptor> parameters = [];

                    if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                    {
                        parameters = parts[2]
                            .Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(param =>
                            {
                                string[] paramParts = param.Split(' ', 2);
                                return new ParameterDescriptor
                                {
                                    ParamType = paramParts[0].Trim(),
                                    ParamName = paramParts.Length > 1 ? paramParts[1].Trim() : "param"
                                };
                            })
                            .ToList();
                    }

                    return new ComponentActionDescriptor
                    {
                        ActionName = parts[0].Trim(),
                        ActionType = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim() + "Action",
                        Parameters = parameters
                    };
                })
                .ToList();
        }

        ComponentGeneratorOptions opts = new()
        {
            Namespace = @namespace,
            RootStateType = rootStateType,
            Components = new List<ComponentDescriptor>
            {
                new ComponentDescriptor
                {
                    ComponentName = componentName,
                    StateSliceName = stateSliceName,
                    StateSliceType = stateSliceType,
                    StateSliceProperty = stateSliceName,
                    Actions = actions
                }
            }
        };

        ComponentGenerator generator = new();
        string code = await generator.GenerateCodeAsync(opts).ConfigureAwait(false);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(code)
                .Header("Generated Code")
                .Expand());
    }

    private static async Task RunStateAsync()
    {
        string @namespace = AnsiConsole.Ask<string>("Namespace?");
        string stateName = AnsiConsole.Ask<string>("State name (e.g., TodoState)?");
        string baseClass = AnsiConsole.Ask<string>("Base class (optional, e.g., IState)?");
        string propertiesCsv = AnsiConsole.Ask<string>("Properties (name:type:default, ...)?");

        List<PropertyDescriptor> properties = [];
        if (!string.IsNullOrWhiteSpace(propertiesCsv))
        {
            properties = propertiesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(prop =>
                {
                    string[] parts = prop.Split(':', 3);
                    return new PropertyDescriptor
                    {
                        PropertyName = parts[0].Trim(),
                        PropertyType = parts.Length > 1 ? parts[1].Trim() : "object",
                        DefaultValue = parts.Length > 2 ? parts[2].Trim() : "default"
                    };
                })
                .ToList();
        }

        StateGeneratorOptions opts = new()
        {
            Namespace = @namespace,
            States = new List<StateDescriptor>
            {
                new StateDescriptor
                {
                    StateName = stateName,
                    BaseClass = string.IsNullOrWhiteSpace(baseClass) ? null : baseClass,
                    Properties = properties
                }
            }
        };

        StateGenerator generator = new();
        string code = await generator.GenerateCodeAsync(opts).ConfigureAwait(false);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(code)
                .Header("Generated Code")
                .Expand());
    }

    private static async Task RunReducerAsync()
    {
        string @namespace = AnsiConsole.Ask<string>("Namespace?");
        string stateType = AnsiConsole.Ask<string>("State type (e.g., TodoState)?");
        string reducerName = AnsiConsole.Ask<string>("Reducer name (e.g., TodoReducer)?");
        string actionsCsv = AnsiConsole.Ask<string>("Actions (comma-separated action names)?");

        string[] actions = actionsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(action => action.Trim())
            .ToArray();

        ReducerGeneratorOptions opts = new()
        {
            Namespace = @namespace,
            Reducers = new List<ReducerDescriptor>
            {
                new ReducerDescriptor
                {
                    ReducerClassName = reducerName,
                    StateType = stateType,
                    Actions = actions
                }
            }
        };

        ReducerGenerator generator = new();
        string code = await generator.GenerateCodeAsync(opts).ConfigureAwait(false);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(code)
                .Header("Generated Code")
                .Expand());
    }

    private static async Task RunEffectsAsync()
    {
        string @namespace = AnsiConsole.Ask<string>("Namespace?");
        string effectName = AnsiConsole.Ask<string>("Effect name (e.g., TodoApiEffect)?");
        string effectType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Effect type:")
                .AddChoices("Async", "Reactive"));
        string triggerActionsCsv = AnsiConsole.Ask<string>("Trigger actions (action1,action2, ...)?");
        string resultActionsCsv = AnsiConsole.Ask<string>("Result actions (optional, action1,action2, ...)?");
        string description = AnsiConsole.Ask<string>("Description (optional)?");
        int timeout = AnsiConsole.Ask("Timeout in milliseconds:", 30000);

        List<string> triggerActions = triggerActionsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(action => action.Trim())
            .ToList();

        List<string> resultActions = [];
        if (!string.IsNullOrWhiteSpace(resultActionsCsv))
        {
            resultActions = resultActionsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(action => action.Trim())
                .ToList();
        }

        EffectsGeneratorOptions opts = new()
        {
            Namespace = @namespace,
            Effects = new List<EffectDescriptor>
            {
                new EffectDescriptor
                {
                    EffectName = effectName,
                    EffectType = effectType == "Async" ? EffectType.Async : EffectType.Reactive,
                    TriggerActions = triggerActions,
                    ResultActions = resultActions,
                    Dependencies = new List<string>(),
                    Summary = string.IsNullOrWhiteSpace(description) ? null : description,
                    TimeoutMs = timeout
                }
            }
        };

        EffectsGenerator generator = new();
        string code = await generator.GenerateCodeAsync(opts).ConfigureAwait(false);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(code)
                .Header("Generated Code")
                .Expand());
    }
}
