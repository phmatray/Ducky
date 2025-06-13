using Demo.ConsoleAppReactive.Services;

namespace Demo.ConsoleAppReactive.Effects;

public class WeatherPollingEffect : PollingEffect<WeatherState>
{
    private readonly IWeatherService _weatherService;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<WeatherPollingEffect> _logger;

    public WeatherPollingEffect(
        IWeatherService weatherService,
        IDispatcher dispatcher,
        ILogger<WeatherPollingEffect> logger)
        : base(TimeSpan.FromSeconds(3))
    {
        _weatherService = weatherService;
        _dispatcher = dispatcher;
        _logger = logger;
        StartImmediately = false;
    }

    protected override IObservable<object> GetStartSignal(IObservable<object> actions)
    {
        return actions.OfActionType<StartWeatherPolling>();
    }

    protected override IObservable<object> GetStopSignal(IObservable<object> actions)
    {
        return actions.OfActionType<StopWeatherPolling>();
    }

    protected override IObservable<object> Poll(WeatherState state)
    {
        return Observable.FromAsync(async () =>
        {
            try
            {
                _logger.LogInformation("Fetching weather for {Location}", state.Location);
                _dispatcher.Dispatch(new WeatherLoading());

                (double temperature, string condition) = await _weatherService
                    .GetWeatherAsync(state.Location)
                    .ConfigureAwait(false);

                return (object)new WeatherLoaded(state.Location, temperature, condition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch weather");
                return (object)new WeatherError(ex.Message);
            }
        });
    }
}
