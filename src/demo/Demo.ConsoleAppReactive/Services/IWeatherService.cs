namespace Demo.ConsoleAppReactive.Services;

public interface IWeatherService
{
    Task<(double Temperature, string Condition)> GetWeatherAsync(string location);
}
