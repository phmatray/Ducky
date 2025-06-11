using Demo.ConsoleApp.Counter;
using Demo.ConsoleApp.Todos;
using Ducky;
using Ducky.Builder;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectre.Console;

AnsiConsole.Write(
    new FigletText("Ducky Demo")
        .Centered()
        .Color(Color.Blue));

// Setup services
ServiceCollection services = [];

// Register slices manually since they have custom implementations
services.AddScoped<ISlice, CounterReducers>();
services.AddScoped<ISlice, TodoReducers>();

// Add Ducky store with builder
services.AddDuckyStore(
    builder => builder
        .AddCorrelationIdMiddleware()
        .AddMiddleware<LoggingMiddleware>()
        .AddAsyncEffectMiddleware()
        .AddEffect<DelayedIncrementEffect>()
        .AddEffect<CounterThresholdEffect>());

ServiceProvider serviceProvider = services.BuildServiceProvider();

// Get store and dispatcher from DI
IStore store = serviceProvider.GetRequiredService<IStore>();
IDispatcher dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

// Subscribe to state changes
void OnStateChanged(object? sender, StateChangedEventArgs e)
{
    CounterState counterState = e.NewState.GetSliceState<CounterState>();
    TodoState todoState = e.NewState.GetSliceState<TodoState>();

    string stateMessage = $"[dim][[State Update]] [/][yellow]Counter: {counterState.Value}[/] | "
        + $"[green]Todos: {todoState.ActiveCount} active[/], [blue]{todoState.CompletedCount} completed[/]";
    AnsiConsole.MarkupLine(stateMessage);
}

store.StateChanged += OnStateChanged;

// Interactive menu
var running = true;
while (running)
{
    AnsiConsole.Clear();

    string choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold blue]Main Menu[/]")
            .PageSize(10)
            .AddChoices("Counter Demo", "Todo List Demo", "Show Current State", "Exit"));

    switch (choice)
    {
        case "Counter Demo":
        {
            await RunCounterDemo(dispatcher, store).ConfigureAwait(false);
            break;
        }
        case "Todo List Demo":
        {
            await RunTodoDemo(dispatcher, store).ConfigureAwait(false);
            break;
        }
        case "Show Current State":
        {
            ShowCurrentState(store.CurrentState);
            AnsiConsole.Prompt(new TextPrompt<string>("[grey]Press Enter to continue...[/]")
                .AllowEmpty());
            break;
        }
        case "Exit":
        {
            running = false;
            break;
        }
    }
}

store.StateChanged -= OnStateChanged;
AnsiConsole.MarkupLine("[green]Goodbye![/]");

async Task RunCounterDemo(IDispatcher actionDispatcher, IStore stateStore)
{
    var counterRunning = true;
    while (counterRunning)
    {
        AnsiConsole.Clear();

        // Get fresh state each time we display
        CounterState counterState = stateStore.CurrentState.GetSliceState<CounterState>();

        Panel panel = new Panel($"[bold yellow]Current Value: {counterState.Value}[/]")
            .Header("[blue]Counter Demo[/]")
            .BorderColor(Color.Blue);
        AnsiConsole.Write(panel);

        string choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select an action:[/]")
                .AddChoices(new[]
                {
                    "Increment (+1)",
                    "Increment (+5)",
                    "Decrement (-1)",
                    "Async Increment (+3 after 2s)",
                    "Set Value",
                    "Reset",
                    "Back to main menu"
                }));

        switch (choice)
        {
            case "Increment (+1)":
            {
                actionDispatcher.Increment();
                break;
            }
            case "Increment (+5)":
            {
                actionDispatcher.Increment(5);
                break;
            }
            case "Decrement (-1)":
            {
                actionDispatcher.Decrement();
                break;
            }
            case "Async Increment (+3 after 2s)":
            {
                actionDispatcher.IncrementAsync(3, 2000);
                await AnsiConsole.Status()
                    .StartAsync(
                        "Starting async increment...",
                        async ctx => { await Task.Delay(500).ConfigureAwait(false); })
                    .ConfigureAwait(false);
                break;
            }
            case "Set Value":
            {
                int value = AnsiConsole.Prompt(
                    new TextPrompt<int>("Enter value:")
                        .ValidationErrorMessage("[red]That's not a valid number[/]"));
                actionDispatcher.SetValue(value);
                break;
            }
            case "Reset":
            {
                actionDispatcher.Reset();
                break;
            }
            case "Back to main menu":
            {
                counterRunning = false;
                break;
            }
        }

        if (counterRunning)
        {
            await Task.Delay(100).ConfigureAwait(false);
        }
    }
}

async Task RunTodoDemo(IDispatcher actionDispatcher, IStore stateStore)
{
    var todoRunning = true;
    while (todoRunning)
    {
        AnsiConsole.Clear();

        TodoState todoState = stateStore.CurrentState.GetSliceState<TodoState>();

        Rule rule = new("[blue]Todo List Demo[/]");
        AnsiConsole.Write(rule);

        AnsiConsole.MarkupLine(
            $"[green]Active: {todoState.ActiveCount}[/] | [blue]Completed: {todoState.CompletedCount}[/]\n");

        ValueCollection<TodoItem> todos = todoState.SelectEntities();
        if (todos.IsEmpty)
        {
            AnsiConsole.MarkupLine("[grey]No todos yet[/]");
        }
        else
        {
            Table table = new();
            table.AddColumn("Status");
            table.AddColumn("ID");
            table.AddColumn("Title");
            table.Border(TableBorder.Rounded);

            foreach (TodoItem todo in todos)
            {
                string status = todo.IsCompleted ? "[green]✓[/]" : "[red]○[/]";
                string title = todo.IsCompleted
                    ? $"[strikethrough grey]{todo.Title}[/]"
                    : todo.Title;
                table.AddRow(status, $"[dim]{todo.Id}[/]", title);
            }

            AnsiConsole.Write(table);
        }

        string choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[bold]Select an action:[/]")
                .AddChoices(new[]
                {
                    "Add Todo",
                    "Toggle Todo",
                    "Remove Todo",
                    "Clear Completed",
                    "Toggle All",
                    "Back to main menu"
                }));

        switch (choice)
        {
            case "Add Todo":
            {
                string title = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter todo title:")
                        .ValidationErrorMessage("[red]Title cannot be empty[/]")
                        .Validate(t => !string.IsNullOrWhiteSpace(t)));
                actionDispatcher.AddTodo(title);
                break;
            }
            case "Toggle Todo":
            {
                if (!todos.IsEmpty)
                {
                    string toggleId = AnsiConsole.Prompt(
                        new TextPrompt<string>("Enter todo ID to toggle:")
                            .ValidationErrorMessage("[red]ID cannot be empty[/]"));
                    actionDispatcher.ToggleTodo(toggleId);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]No todos to toggle[/]");
                    await Task.Delay(1000).ConfigureAwait(false);
                }

                break;
            }
            case "Remove Todo":
            {
                if (!todos.IsEmpty)
                {
                    string removeId = AnsiConsole.Prompt(
                        new TextPrompt<string>("Enter todo ID to remove:")
                            .ValidationErrorMessage("[red]ID cannot be empty[/]"));
                    actionDispatcher.RemoveTodo(removeId);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]No todos to remove[/]");
                    await Task.Delay(1000).ConfigureAwait(false);
                }

                break;
            }
            case "Clear Completed":
            {
                actionDispatcher.ClearCompleted();
                break;
            }
            case "Toggle All":
            {
                bool markCompleted = AnsiConsole.Confirm("Mark all as completed?");
                actionDispatcher.ToggleAll(markCompleted);
                break;
            }
            case "Back to main menu":
            {
                todoRunning = false;
                break;
            }
        }

        if (todoRunning)
        {
            await Task.Delay(100).ConfigureAwait(false);
        }
    }
}

void ShowCurrentState(IRootState rootState)
{
    CounterState counterState = rootState.GetSliceState<CounterState>();
    TodoState todoState = rootState.GetSliceState<TodoState>();

    Panel statePanel = new Panel(
        new Rows(
            new Markup($"[yellow]Counter Value:[/] {counterState.Value}"),
            new Markup($"[green]Active Todos:[/] {todoState.ActiveCount}"),
            new Markup($"[blue]Completed Todos:[/] {todoState.CompletedCount}")))
        .Header("[bold]Current State[/]")
        .BorderColor(Color.Green);

    AnsiConsole.Write(statePanel);

    ValueCollection<TodoItem> todos = todoState.SelectEntities();
    if (todos.IsEmpty)
    {
        return;
    }

    AnsiConsole.WriteLine();
    Table todoTable = new();
    todoTable.Title("[underline]Todo Items[/]");
    todoTable.AddColumn("Status");
    todoTable.AddColumn("Title");
    todoTable.Border(TableBorder.Simple);

    foreach (TodoItem todo in todos)
    {
        string status = todo.IsCompleted ? "[green]✓[/]" : "[red]○[/]";
        string title = todo.IsCompleted
            ? $"[strikethrough grey]{todo.Title}[/]"
            : todo.Title;
        todoTable.AddRow(status, title);
    }

    AnsiConsole.Write(todoTable);
}

// Custom middleware for demonstration
public sealed class LoggingMiddleware : IMiddleware
{
    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        return Task.CompletedTask;
    }

    public void AfterInitializeAllMiddlewares()
    {
        // Nothing to do
    }

    public bool MayDispatchAction(object action)
    {
        return true;
    }

    public void BeforeDispatch(object action)
    {
        AnsiConsole.MarkupLine($"[dim][[Middleware]] Before: {action.GetType().Name}[/]");
    }

    public void AfterDispatch(object action)
    {
        AnsiConsole.MarkupLine($"[dim][[Middleware]] After: {action.GetType().Name}[/]");
    }

    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { });
    }
}
