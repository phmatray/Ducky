namespace Demo.ConsoleAppReactive.Services;

public interface IStockService
{
    IObservable<decimal> StreamPricesAsync(string symbol);
}
