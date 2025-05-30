using Ducky.CodeGen.Core;
using Spectre.Console;

namespace Ducky.CodeGen.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("CodeGen")
                .Color(Color.Green));

        // 1) Let user pick a generator
        var generators = new[]
        {
            ((string Name, Func<Task>))("Action Creator", RunActionCreatorAsync)
            // add more here…
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
}
