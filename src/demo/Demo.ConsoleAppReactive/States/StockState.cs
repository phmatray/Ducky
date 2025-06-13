using System.Collections.Immutable;

namespace Demo.ConsoleAppReactive.States;

// Stock state for streaming effect demo
public record StockState : IState
{
    public ImmutableDictionary<string, StockPrice> Stocks { get; init; } = ImmutableDictionary<string, StockPrice>.Empty;
}

public record StockPrice(
    string Symbol,
    decimal Price,
    decimal Change,
    DateTime LastUpdate);

// Stock actions
[DuckyAction]
public record AddStockToWatch(string Symbol);

[DuckyAction]
public record RemoveStockFromWatch(string Symbol);

[DuckyAction]
public record UpdateStockPrice(string Symbol, decimal Price);

// Stock slice reducers
public record StockSliceReducers : SliceReducers<StockState>
{
    public override StockState GetInitialState() => new();

    public StockSliceReducers()
    {
        On<AddStockToWatch>((state, action) =>
        {
            if (state.Stocks.ContainsKey(action.Symbol))
            {
                return state;
            }

            StockPrice initialPrice = new(action.Symbol, 100m, 0m, DateTime.UtcNow);
            return state with
            {
                Stocks = state.Stocks.Add(action.Symbol, initialPrice)
            };
        });

        On<RemoveStockFromWatch>((state, action) => state with
            {
                Stocks = state.Stocks.Remove(action.Symbol)
            });

        On<UpdateStockPrice>((state, action) =>
        {
            if (!state.Stocks.TryGetValue(action.Symbol, out StockPrice? currentStock))
            {
                return state;
            }

            decimal change = action.Price - currentStock.Price;
            StockPrice updatedStock = currentStock with
            {
                Price = action.Price,
                Change = change,
                LastUpdate = DateTime.UtcNow
            };

            return state with
            {
                Stocks = state.Stocks.SetItem(action.Symbol, updatedStock)
            };
        });
    }
}
