using Demo.ConsoleApp.Counter;
using Demo.ConsoleApp.Todos;
using Ducky;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using R3;

Console.WriteLine("=== Ducky Console Demo ===\n");

// Setup dispatcher and store
Dispatcher dispatcher = new();

// Create reducers
CounterReducers counterReducers = new();
TodoReducers todoReducers = new();

// Setup services for async effects
ServiceCollection services = [];
services.AddSingleton<IAsyncEffect, DelayedIncrementEffect>();
services.AddSingleton<IAsyncEffect, CounterThresholdEffect>();

ServiceProvider serviceProvider = services.BuildServiceProvider();

// Create a variable to hold the store reference
DuckyStore? storeRef = null;

// Create store with middleware pipeline
DuckyStore store = DuckyStoreFactory.CreateStore(
    dispatcher,
    [counterReducers, todoReducers],
    pipeline =>
    {
        pipeline.Use(new LoggingMiddleware());
        pipeline.Use(new AsyncEffectMiddleware(
            serviceProvider,
            () => storeRef!.CurrentState,
            dispatcher));
    });

// Set the store reference
storeRef = store;

// Subscribe to state changes
IDisposable subscription = store.RootStateObservable
    .Subscribe(rootState =>
    {
        CounterState counterState = rootState.GetSliceState<CounterState>();
        TodoState todoState = rootState.GetSliceState<TodoState>();

        string msg = $"\n[State Update] Counter: {counterState.Value} | ";
        msg += $"Todos: {todoState.ActiveCount} active, {todoState.CompletedCount} completed";
        Console.WriteLine(msg);
    });

// Interactive menu
var running = true;
while (running)
{
    Console.WriteLine("\n--- Main Menu ---");
    Console.WriteLine("1. Counter Demo");
    Console.WriteLine("2. Todo List Demo");
    Console.WriteLine("3. Show Current State");
    Console.WriteLine("0. Exit");
    Console.Write("\nSelect option: ");

    string? choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
        {
            await RunCounterDemo(dispatcher, store.CurrentState).ConfigureAwait(false);
            break;
        }
        case "2":
        {
            await RunTodoDemo(dispatcher, store.CurrentState).ConfigureAwait(false);
            break;
        }
        case "3":
        {
            ShowCurrentState(store.CurrentState);
            break;
        }
        case "0":
        {
            running = false;
            break;
        }
        default:
            {
                Console.WriteLine("Invalid option!");
                break;
            }
    }
}

subscription.Dispose();
Console.WriteLine("\nGoodbye!");

async Task RunCounterDemo(IDispatcher dispatcher, IRootState rootState)
{
    var counterRunning = true;
    while (counterRunning)
    {
        CounterState counterState = rootState.GetSliceState<CounterState>();

        Console.WriteLine($"\n--- Counter Demo (Current: {counterState.Value}) ---");
        Console.WriteLine("1. Increment (+1)");
        Console.WriteLine("2. Increment (+5)");
        Console.WriteLine("3. Decrement (-1)");
        Console.WriteLine("4. Async Increment (+3 after 2s)");
        Console.WriteLine("5. Set Value");
        Console.WriteLine("6. Reset");
        Console.WriteLine("0. Back to main menu");
        Console.Write("\nSelect option: ");

        string? choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                {
                    dispatcher.Increment();
                    break;
                }
            case "2":
                {
                    dispatcher.Increment(5);
                    break;
                }
            case "3":
                {
                    dispatcher.Decrement();
                    break;
                }
            case "4":
                {
                    dispatcher.IncrementAsync(3, 2000);
                    Console.WriteLine("Async increment started...");
                    break;
                }
            case "5":
            {
                Console.Write("Enter value: ");
                if (int.TryParse(Console.ReadLine(), out int value))
                {
                    dispatcher.SetValue(value);
                }
                else
                {
                    Console.WriteLine("Invalid number!");
                }

                break;
            }
            case "6":
                {
                    dispatcher.Reset();
                    break;
                }
            case "0":
                {
                    counterRunning = false;
                    break;
                }
            default:
                {
                    Console.WriteLine("Invalid option!");
                    break;
                }
        }

        await Task.Delay(100).ConfigureAwait(false);
    }
}

async Task RunTodoDemo(IDispatcher dispatcher, IRootState rootState)
{
    var todoRunning = true;
    while (todoRunning)
    {
        TodoState todoState = rootState.GetSliceState<TodoState>();

        Console.WriteLine($"\n--- Todo List Demo ---");
        Console.WriteLine($"Active: {todoState.ActiveCount} | Completed: {todoState.CompletedCount}");
        Console.WriteLine("\nTodos:");

        ValueCollection<TodoItem> todos = todoState.SelectEntities();
        if (todos.IsEmpty)
        {
            Console.WriteLine("  (No todos yet)");
        }
        else
        {
            foreach (TodoItem todo in todos)
            {
                string status = todo.IsCompleted ? "[X]" : "[ ]";
                Console.WriteLine($"  {status} {todo.Title} (ID: {todo.Id})");
            }
        }

        Console.WriteLine("\nOptions:");
        Console.WriteLine("1. Add Todo");
        Console.WriteLine("2. Toggle Todo");
        Console.WriteLine("3. Remove Todo");
        Console.WriteLine("4. Clear Completed");
        Console.WriteLine("5. Toggle All");
        Console.WriteLine("0. Back to main menu");
        Console.Write("\nSelect option: ");

        string? choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
            {
                Console.Write("Enter todo title: ");
                string? title = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    dispatcher.AddTodo(title);
                }

                break;
            }
            case "2":
            {
                Console.Write("Enter todo ID to toggle: ");
                string? toggleId = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(toggleId))
                {
                    dispatcher.ToggleTodo(toggleId);
                }

                break;
            }
            case "3":
            {
                Console.Write("Enter todo ID to remove: ");
                string? removeId = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(removeId))
                {
                    dispatcher.RemoveTodo(removeId);
                }

                break;
            }
            case "4":
                {
                    dispatcher.ClearCompleted();
                    break;
                }
            case "5":
            {
                Console.Write("Mark all as completed? (y/n): ");
                bool markCompleted = Console.ReadLine()?.ToLower() == "y";
                dispatcher.ToggleAll(markCompleted);
                break;
            }
            case "0":
                {
                    todoRunning = false;
                    break;
                }
            default:
                {
                    Console.WriteLine("Invalid option!");
                    break;
                }
        }

        await Task.Delay(100).ConfigureAwait(false);
    }
}

void ShowCurrentState(IRootState rootState)
{
    CounterState counterState = rootState.GetSliceState<CounterState>();
    TodoState todoState = rootState.GetSliceState<TodoState>();

    Console.WriteLine("\n=== Current State ===");
    Console.WriteLine($"Counter Value: {counterState.Value}");
    string summary = $"Todo Summary: {todoState.ActiveCount} active, ";
    summary += $"{todoState.CompletedCount} completed";
    Console.WriteLine(summary);

    ValueCollection<TodoItem> todos = todoState.SelectEntities();
    if (todos.IsEmpty)
    {
        return;
    }

    Console.WriteLine("\nTodo Items:");
    foreach (TodoItem todo in todos)
    {
        string status = todo.IsCompleted ? "[X]" : "[ ]";
        Console.WriteLine($"  {status} {todo.Title}");
    }
}

// Custom middleware for demonstration
public sealed class LoggingMiddleware : IActionMiddleware
{
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        return actions.Do(context =>
        {
            Console.WriteLine($"[Middleware] Before: {context.Action.GetType().Name}");
        });
    }

    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        return actions.Do(context =>
        {
            Console.WriteLine($"[Middleware] After: {context.Action.GetType().Name}");
        });
    }
}
