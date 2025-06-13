using Ducky.Reactive;
using Demo.ConsoleAppReactive.Services;

namespace Demo.ConsoleAppReactive.Effects;

public class StockStreamingEffect : ReactiveEffectBase
{
    private readonly IStockService _stockService;
    private readonly ILogger<StockStreamingEffect> _logger;

    public StockStreamingEffect(
        IStockService stockService,
        ILogger<StockStreamingEffect> logger)
    {
        _stockService = stockService;
        _logger = logger;
    }

    protected override IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IRootState> rootState)
    {
        // Start streaming when stock is added to watch list
        IObservable<object> startStreaming = actions
            .OfActionType<AddStockToWatch>()
            .SelectMany(action => 
                _stockService.StreamPricesAsync(action.Symbol)
                    .Select(price => new UpdateStockPrice(action.Symbol, price))
                    .TakeUntil(actions.OfActionType<RemoveStockFromWatch>()
                        .Where(remove => remove.Symbol == action.Symbol))
                    .Catch<object, Exception>(ex =>
                    {
                        _logger.LogError(ex, "Error streaming prices for {Symbol}", action.Symbol);
                        return Observable.Empty<object>();
                    })
            );

        return startStreaming;
    }
}
