using Demo.ConsoleAppReactive.Services;

namespace Demo.ConsoleAppReactive;

public class MockWeatherService : IWeatherService
{
    private readonly Random _random = new();
    private readonly string[] _conditions = ["Sunny", "Cloudy", "Rainy", "Snowy", "Windy", "Stormy"];

    public async Task<(double Temperature, string Condition)> GetWeatherAsync(string location)
    {
        // Simulate API delay
        await Task.Delay(500 + _random.Next(1000)).ConfigureAwait(false);
        
        // Simulate occasional errors
        if (_random.Next(10) == 0)
        {
            throw new($"Failed to fetch weather data for {location}");
        }

        // Generate random weather data
        double temperature = Math.Round(15 + (_random.NextDouble() * 20) - 10, 1);
        string condition = _conditions[_random.Next(_conditions.Length)];

        return (temperature, condition);
    }
}
