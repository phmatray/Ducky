namespace Demo.ConsoleAppReactive.States;

// Weather state for polling effect demo
public record WeatherState : IState
{
    public string Location { get; init; } = "Unknown";
    public double Temperature { get; init; }
    public string Condition { get; init; } = "Unknown";
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
    public bool IsLoading { get; init; }
    public string? Error { get; init; }
}

// Weather actions
[DuckyAction]
public record StartWeatherPolling(string Location);

[DuckyAction]
public record StopWeatherPolling;

[DuckyAction]
public record WeatherLoading;

[DuckyAction]
public record WeatherLoaded(string Location, double Temperature, string Condition);

[DuckyAction]
public record WeatherError(string Message);

// Weather slice reducers
public record WeatherSliceReducers : SliceReducers<WeatherState>
{
    public override WeatherState GetInitialState() => new();

    public WeatherSliceReducers()
    {
        On<StartWeatherPolling>((state, action) => state with 
            { 
                Location = action.Location,
                IsLoading = false,
                Error = null 
            });

        On<WeatherLoading>(state => state with { IsLoading = true, Error = null });

        On<WeatherLoaded>((state, action) => state with
            {
                Location = action.Location,
                Temperature = action.Temperature,
                Condition = action.Condition,
                LastUpdated = DateTime.UtcNow,
                IsLoading = false,
                Error = null
            });

        On<WeatherError>((state, action) => state with
            {
                IsLoading = false,
                Error = action.Message
            });
    }
}
