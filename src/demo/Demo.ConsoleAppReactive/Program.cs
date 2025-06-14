#pragma warning disable RCS1090 // Add 'ConfigureAwait(false)' call
#pragma warning disable RCS1250 // Simplify object creation  
#pragma warning disable RCS1264 // Use explicit type instead of 'var'
#pragma warning disable RCS0008 // Add blank line between closing brace and next statement

using Demo.ConsoleAppReactive;
using Demo.ConsoleAppReactive.Services;

// Configure services for Ducky.Reactive demo
ServiceCollection services = [];

services.AddLogging(builder =>
    builder.AddConsole()
        .SetMinimumLevel(LogLevel.Warning)
        .AddFilter("Ducky", LogLevel.None)
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning));

// Add mock services
services.AddSingleton<IWeatherService, MockWeatherService>();
services.AddSingleton<IStockService, MockStockService>();

// Add state slices manually - register both specific and generic interfaces
services.AddScoped<WeatherSliceReducers>();
services.AddScoped<ISlice<WeatherState>>(sp => sp.GetRequiredService<WeatherSliceReducers>());
services.AddScoped<ISlice>(sp => sp.GetRequiredService<WeatherSliceReducers>());

services.AddScoped<StockSliceReducers>();
services.AddScoped<ISlice<StockState>>(sp => sp.GetRequiredService<StockSliceReducers>());
services.AddScoped<ISlice>(sp => sp.GetRequiredService<StockSliceReducers>());

services.AddScoped<SearchSliceReducers>();
services.AddScoped<ISlice<SearchState>>(sp => sp.GetRequiredService<SearchSliceReducers>());
services.AddScoped<ISlice>(sp => sp.GetRequiredService<SearchSliceReducers>());

services.AddScoped<NotificationSliceReducers>();
services.AddScoped<ISlice<NotificationState>>(sp => sp.GetRequiredService<NotificationSliceReducers>());
services.AddScoped<ISlice>(sp => sp.GetRequiredService<NotificationSliceReducers>());

// Configure Ducky store
services.AddDuckyStore(builder => builder
    .UseDefaultMiddlewares());

ServiceProvider serviceProvider = services.BuildServiceProvider();
IDispatcher dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
IStore store = serviceProvider.GetRequiredService<IStore>();

// ASCII Art Header
AnsiConsole.Write(
    new FigletText("Ducky.Reactive")
        .Centered()
        .Color(Color.Cyan1));

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[bold cyan]Reactive Effects Demo[/]");
AnsiConsole.MarkupLine("[dim]Showcasing reactive patterns, effects, and observable extensions[/]");
AnsiConsole.WriteLine();

// Direct test of weather polling to debug the issue
AnsiConsole.MarkupLine("[yellow]Testing weather polling directly...[/]");
string choice = "Weather Polling Effect - Start real-time weather updates";

// Interactive demo loop would go here, but we're testing directly
if (true)
{
    switch (choice)
    {
        case "Weather Polling Effect - Start real-time weather updates":
        {
            await RunWeatherPollingDemo(dispatcher, store, serviceProvider).ConfigureAwait(false);
            break;
        }
        case "Stock Streaming Effect - Live stock price feeds":
        {
            await RunStockStreamingDemo(dispatcher, store).ConfigureAwait(false);
            break;
        }
        case "Search Debounced Effect - Demonstrate search debouncing":
        {
            await RunSearchDebouncedDemo(dispatcher).ConfigureAwait(false);
            break;
        }
        case "Notification Workflow Effect - Complex async workflows":
        {
            await RunNotificationWorkflowDemo(dispatcher, store).ConfigureAwait(false);
            break;
        }
        case "Observable Extensions Test - Rate limiting, retry, batching":
        {
            await RunObservableExtensionsDemo().ConfigureAwait(false);
            break;
        }
        case "Error Recovery Demo - Simulate and recover from errors":
        {
            await RunErrorRecoveryDemo(dispatcher).ConfigureAwait(false);
            break;
        }
        case "View Effect Metrics - Performance monitoring":
        {
            AnsiConsole.MarkupLine("[yellow]Effect metrics monitoring has been simplified for this demo.[/]");
            break;
        }
        case "View All States - Current reactive state":
        {
            ViewAllStates(store);
            break;
        }
        case "Exit":
        {
            AnsiConsole.MarkupLine("[green]Thanks for exploring Ducky.Reactive![/]");
            return;
        }
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[green]Weather polling test completed![/]");
}

static async Task RunWeatherPollingDemo(IDispatcher dispatcher, IStore store, ServiceProvider serviceProvider)
{
    AnsiConsole.Write(new Rule("[cyan]Weather Polling Effect Demo[/]"));
    AnsiConsole.MarkupLine("This demonstrates a [bold]PollingEffect[/] that fetches weather data every 3 seconds.");
    AnsiConsole.WriteLine();

    // Initialize weather state
    dispatcher.Dispatch(new StartWeatherPolling("New York"));

    // Debug: Check if weather state is available
    try
    {
        WeatherState initialState = store.GetSlice<WeatherState>();
        AnsiConsole.MarkupLine($"[green]Weather state initialized: {initialState.Location}[/]");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error getting weather state: {ex.Message}[/]");
        AnsiConsole.MarkupLine("[yellow]Available state slices:[/]");
        // Add some debugging to see what slices are available
        return;
    }

    var weatherService = serviceProvider.GetRequiredService<IWeatherService>();
    var cancellationTokenSource = new CancellationTokenSource();

    // Do an initial fetch immediately
    try
    {
        AnsiConsole.MarkupLine("[yellow]Dispatching WeatherLoading...[/]");
        dispatcher.Dispatch(new WeatherLoading());

        AnsiConsole.MarkupLine("[yellow]Calling weather service...[/]");
        var (temperature, condition) = await weatherService.GetWeatherAsync("New York");

        AnsiConsole.MarkupLine($"[yellow]Dispatching WeatherLoaded: {temperature}°C, {condition}[/]");
        dispatcher.Dispatch(new WeatherLoaded("New York", temperature, condition));

        AnsiConsole.MarkupLine("[yellow]Weather loaded successfully![/]");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        dispatcher.Dispatch(new WeatherError(ex.Message));
    }

    // Start background polling task for subsequent updates
    var pollingTask = Task.Run(async () =>
    {
        try
        {
            // Wait initial delay before first poll
            await Task.Delay(3000, cancellationTokenSource.Token);

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    dispatcher.Dispatch(new WeatherLoading());

                    var (temperature, condition) = await weatherService.GetWeatherAsync("New York");
                    dispatcher.Dispatch(new WeatherLoaded("New York", temperature, condition));
                }
                catch (Exception ex)
                {
                    dispatcher.Dispatch(new WeatherError(ex.Message));
                }

                await Task.Delay(3000, cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
    });

    // Show live updates for 20 seconds (to see multiple polling cycles)
    Panel weatherPanel = new("Initializing weather polling...")
    {
        Header = new PanelHeader("[cyan]Live Weather Updates[/]"),
        BorderStyle = Style.Parse("cyan")
    };

#pragma warning disable RCS0054 // Fix formatting of a call chain
    await AnsiConsole.Live(weatherPanel).StartAsync(async ctx =>
    {
        // Update display every 500ms for more responsive updates
        for (int i = 0; i < 40; i++) // 40 * 500ms = 20 seconds
        {
            WeatherState state = store.GetSlice<WeatherState>();

            if (state.IsLoading)
            {
                weatherPanel = new Panel(new Markup("[yellow]Fetching weather data...[/]"))
                {
                    Header = new PanelHeader("[cyan]Live Weather Updates[/]"),
                    BorderStyle = Style.Parse("cyan")
                };
            }
            else if (state.Error is not null)
            {
                weatherPanel = new Panel(new Markup($"[red]Error: {Markup.Escape(state.Error)}[/]"))
                {
                    Header = new PanelHeader("[cyan]Live Weather Updates[/]"),
                    BorderStyle = Style.Parse("cyan")
                };
            }
            else if (state.Temperature > 0 || !string.IsNullOrEmpty(state.Condition))
            {
                Grid content = new Grid()
                    .AddColumn()
                    .AddColumn()
                    .AddRow("[bold]Location:[/]", state.Location)
                    .AddRow("[bold]Temperature:[/]", $"{state.Temperature:F1}°C")
                    .AddRow("[bold]Condition:[/]", state.Condition)
                    .AddRow("[bold]Last Updated:[/]", state.LastUpdated.ToString("HH:mm:ss"))
                    .AddRow("[bold]Polls Completed:[/]", $"{(i * 500 / 3000) + 1}");

                weatherPanel = new Panel(content)
                {
                    Header = new PanelHeader("[cyan]Live Weather Updates[/]"),
                    BorderStyle = Style.Parse("cyan")
                };
            }
            else
            {
                weatherPanel = new Panel(new Markup("[dim]Waiting for first weather update...[/]"))
                {
                    Header = new PanelHeader("[cyan]Live Weather Updates[/]"),
                    BorderStyle = Style.Parse("cyan")
                };
            }

            ctx.Refresh();
            await Task.Delay(500).ConfigureAwait(false); // More frequent updates
        }
    }).ConfigureAwait(false);
#pragma warning restore RCS0054 // Fix formatting of a call chain

    // Stop polling
    cancellationTokenSource.Cancel();
    try
    {
        await pollingTask;
    }
    catch (OperationCanceledException)
    {
        // Expected
    }

    AnsiConsole.MarkupLine("[green]Weather polling stopped.[/]");
}

static async Task RunStockStreamingDemo(IDispatcher dispatcher, IStore store)
{
    AnsiConsole.Write(new Rule("[cyan]Stock Streaming Effect Demo[/]"));
    AnsiConsole.MarkupLine(
        "This demonstrates [bold]ReactiveEffect[/] with real-time streaming data and rate limiting.");
    AnsiConsole.WriteLine();

    // Add stocks to watch
    string[] symbols = ["AAPL", "GOOGL", "MSFT"];
    foreach (string symbol in symbols)
    {
        dispatcher.Dispatch(new AddStockToWatch(symbol));
    }

    Table table = new Table()
        .AddColumn("Symbol")
        .AddColumn("Price")
        .AddColumn("Change")
        .AddColumn("Last Update");

    await AnsiConsole.Live(table)
        .StartAsync(async ctx =>
        {
            for (int i = 0; i < 20; i++)
            {
                table.Rows.Clear();

                StockState stockState = store.GetSlice<StockState>();

                foreach (StockPrice stock in stockState.Stocks.Values)
                {
                    string changeColor = stock.Change >= 0 ? "green" : "red";
                    table.AddRow(
                        stock.Symbol,
                        $"${stock.Price:F2}",
                        $"[{changeColor}]{stock.Change:+0.00;-0.00}[/]",
                        stock.LastUpdate.ToString("HH:mm:ss")
                    );
                }

                ctx.Refresh();
                await Task.Delay(500).ConfigureAwait(false);
            }
        })
        .ConfigureAwait(false);

    // Remove stocks
    foreach (string symbol in symbols)
    {
        dispatcher.Dispatch(new RemoveStockFromWatch(symbol));
    }

    AnsiConsole.MarkupLine("[green]Stock streaming stopped.[/]");
}

static async Task RunSearchDebouncedDemo(IDispatcher dispatcher)
{
    AnsiConsole.Write(new Rule("[cyan]Search Debounced Effect Demo[/]"));
    AnsiConsole.MarkupLine("This demonstrates [bold]DebouncedEffect[/] that waits 500ms before executing search.");
    AnsiConsole.WriteLine();

    string[] searchTerms = ["D", "Du", "Duc", "Duck", "Ducky", "Ducky.Reactive"];

    foreach (string term in searchTerms)
    {
        dispatcher.Dispatch(new SearchQuery(term));
        AnsiConsole.MarkupLine($"[dim]Typed: '{term}'[/]");
        await Task.Delay(100).ConfigureAwait(false); // Simulate fast typing
    }

    AnsiConsole.MarkupLine("[yellow]Waiting for debounced search to execute...[/]");
    await Task.Delay(600).ConfigureAwait(false); // Wait for debounce

    AnsiConsole.MarkupLine("[green]Search executed for final term 'Ducky.Reactive'![/]");
}

static async Task RunNotificationWorkflowDemo(IDispatcher dispatcher, IStore store)
{
    AnsiConsole.Write(new Rule("[cyan]Notification Workflow Effect Demo[/]"));
    AnsiConsole.MarkupLine("This demonstrates [bold]WorkflowEffect[/] with multi-step async workflows.");
    AnsiConsole.WriteLine();

    // Start a complex workflow
    dispatcher.Dispatch(new StartNotificationWorkflow("Welcome", "Welcome to Ducky.Reactive!", NotificationType.Info));

    await AnsiConsole.Progress()
        .StartAsync(async ctx =>
        {
            ProgressTask task = ctx.AddTask("[green]Executing workflow...[/]");

            for (int i = 0; i <= 100; i += 10)
            {
                task.Value = i;
                await Task.Delay(200).ConfigureAwait(false);
            }
        })
        .ConfigureAwait(false);

    NotificationState notificationState = store.GetSlice<NotificationState>();
    AnsiConsole.MarkupLine($"[green]Workflow completed! {notificationState.Notifications.Count} notifications processed.[/]");
}

static async Task RunObservableExtensionsDemo()
{
    AnsiConsole.Write(new Rule("[cyan]Observable Extensions Demo[/]"));
    AnsiConsole.MarkupLine("This demonstrates the enhanced observable extensions we built.");
    AnsiConsole.WriteLine();

    // Rate limiting demo
    AnsiConsole.MarkupLine("[yellow]Rate Limiting Demo (min 200ms between items):[/]");
    IObservable<long> rateLimited = Observable.Interval(TimeSpan.FromMilliseconds(50))
        .Take(5)
        .RateLimit(TimeSpan.FromMilliseconds(200));

    await rateLimited
        .ForEachAsync(x => AnsiConsole.MarkupLine($"[green]Item {x} at {DateTime.Now:HH:mm:ss.fff}[/]"))
        .ConfigureAwait(false);

    AnsiConsole.WriteLine();

    // Retry with backoff demo
    AnsiConsole.MarkupLine("[yellow]Retry with Backoff Demo:[/]");
    int attempts = 0;
    IObservable<string> retryDemo = Observable.Defer(() =>
    {
        attempts++;
        if (attempts <= 2)
        {
            AnsiConsole.MarkupLine($"[red]Attempt {attempts} failed[/]");
            return Observable.Throw<string>(new Exception($"Attempt {attempts} failed"));
        }

        AnsiConsole.MarkupLine($"[green]Attempt {attempts} succeeded![/]");
        return Observable.Return("Success!");
    });

    string result = await retryDemo
        .RetryWithBackoff(3, TimeSpan.FromMilliseconds(100))
        .Catch<string, Exception>(ex => Observable.Return($"Failed: {ex.Message}"));

    AnsiConsole.MarkupLine($"[cyan]Final result: {result}[/]");
}

static async Task RunErrorRecoveryDemo(IDispatcher dispatcher)
{
    AnsiConsole.Write(new Rule("[cyan]Error Recovery Demo[/]"));
    AnsiConsole.MarkupLine("This demonstrates error handling and recovery strategies.");
    AnsiConsole.WriteLine();

    string[] errorTypes = ["network", "timeout", "validation"];

    foreach (string errorType in errorTypes)
    {
        AnsiConsole.MarkupLine($"[yellow]Simulating {errorType} error...[/]");
        dispatcher.Dispatch(new SimulateError(errorType));
        await Task.Delay(1000).ConfigureAwait(false);
    }

    AnsiConsole.MarkupLine("[green]All errors handled and recovered![/]");
}


static void ViewAllStates(IStore store)
{
    AnsiConsole.Write(new Rule("[cyan]Current Reactive States[/]"));

    Tree tree = new("[yellow]Store State[/]");

    try
    {
        WeatherState weather = store.GetSlice<WeatherState>();
        TreeNode weatherNode = tree.AddNode("[cyan]Weather State[/]");
        weatherNode.AddNode($"Location: {weather.Location}");
        weatherNode.AddNode($"Temperature: {weather.Temperature:F1}°C");
        weatherNode.AddNode($"Condition: {weather.Condition}");
        weatherNode.AddNode($"Loading: {weather.IsLoading}");
    }
    catch
    {
        tree.AddNode("[dim]Weather State: Not initialized[/]");
    }

    try
    {
        StockState stocks = store.GetSlice<StockState>();
        TreeNode stockNode = tree.AddNode("[cyan]Stock State[/]");
        stockNode.AddNode($"Watching: {stocks.Stocks.Count} stocks");
        foreach (StockPrice stock in stocks.Stocks.Values.Take(3))
        {
            stockNode.AddNode($"{stock.Symbol}: ${stock.Price:F2}");
        }
    }
    catch
    {
        tree.AddNode("[dim]Stock State: Not initialized[/]");
    }

    try
    {
        SearchState search = store.GetSlice<SearchState>();
        TreeNode searchNode = tree.AddNode("[cyan]Search State[/]");
        searchNode.AddNode($"Query: '{search.Query}'");
        searchNode.AddNode($"Results: {search.ResultCount}");
        searchNode.AddNode($"Loading: {search.IsLoading}");
    }
    catch
    {
        tree.AddNode("[dim]Search State: Not initialized[/]");
    }

    AnsiConsole.Write(tree);
}
