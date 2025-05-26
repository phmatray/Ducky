using Ducky.Middlewares.AsyncEffect;
using Spectre.Console;

namespace Demo.ConsoleApp.Counter;

public sealed class DelayedIncrementEffect : AsyncEffect<IncrementAsync>
{
    public override async Task HandleAsync(IncrementAsync action, IRootState rootState)
    {
        AnsiConsole.MarkupLine(
            $"[cyan][[Effect]][/] Starting delayed increment of [yellow]{action.Amount}[/] after [blue]{action.DelayMs}ms[/]...");

        await Task.Delay(action.DelayMs).ConfigureAwait(false);

        AnsiConsole.MarkupLine($"[cyan][[Effect]][/] Delay complete, incrementing by [yellow]{action.Amount}[/]");
        Dispatcher.Increment(action.Amount);
    }
}

public sealed class CounterThresholdEffect : AsyncEffect<Increment>
{
    public override async Task HandleAsync(Increment action, IRootState rootState)
    {
        CounterState counterState = rootState.GetSliceState<CounterState>();

        if (counterState.Value >= 10)
        {
            AnsiConsole.MarkupLine("[cyan][[Effect]][/] [bold yellow]Counter reached 10![/] Triggering celebration...");
            await Task.Delay(500).ConfigureAwait(false);
            AnsiConsole.MarkupLine("[cyan][[Effect]][/] [bold green]🎉 Congratulations! You've reached 10![/]");
        }

        if (counterState.Value < 20)
        {
            return;
        }

        AnsiConsole.MarkupLine("[cyan][[Effect]][/] [bold red]Counter is getting high! Consider resetting.[/]");
    }
}
