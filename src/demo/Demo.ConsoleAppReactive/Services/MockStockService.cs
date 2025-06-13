namespace Demo.ConsoleAppReactive.Services;

public class MockStockService : IStockService
{
    private readonly Random _random = new();

    private readonly Dictionary<string, decimal> _basePrices = new()
    {
        ["AAPL"] = 150.00m,
        ["GOOGL"] = 2800.00m,
        ["MSFT"] = 300.00m
    };

    public IObservable<decimal> StreamPricesAsync(string symbol)
    {
        return Observable.Create<decimal>(observer =>
        {
            decimal basePrice = _basePrices.GetValueOrDefault(symbol, 100m);
            decimal currentPrice = basePrice;
            
            IDisposable timer = Observable.Interval(TimeSpan.FromMilliseconds(500 + _random.Next(1000)))
                .Subscribe(_ =>
                {
                    // Simulate price movement
                    var change = (decimal)(_random.NextDouble() * 0.04 - 0.02); // +/- 2%
                    currentPrice = Math.Max(1m, currentPrice * (1 + change));
                    observer.OnNext(Math.Round(currentPrice, 2));
                });
                
            return timer;
        });
    }
}
