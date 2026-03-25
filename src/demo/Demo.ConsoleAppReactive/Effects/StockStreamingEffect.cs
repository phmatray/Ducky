// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

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
        IObservable<IStateProvider> stateProvider)
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
