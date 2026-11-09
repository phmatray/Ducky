// A MayDispatch veto: QuantityLimitMiddleware refuses an AddToBasket that breaks a shop
// rule; the components that awaited the dispatch explain it with the same rule.
using Ducky.Playground.Shop.Catalogue;

namespace Ducky.Playground.Shop.Basket;

public static class ShopRules
{
    public const int MaxPerDuck = 10;

    // Why one more of this duck can't go in the basket, or null when it can.
    public static string? RefusalReason(StateSnapshot state, string duckId)
    {
        var inBasket = state.Get<BasketState>().QuantityOf(duckId);
        var duck = state.Get<CatalogueState>().Ducks.Find(duckId);
        if (inBasket >= MaxPerDuck)
        {
            return $"{MaxPerDuck} of one duck is our limit.";
        }
        if (duck is not null && inBasket >= duck.Stock)
        {
            return duck.Stock == 0
                ? $"{duck.Name} is sold out."
                : $"Only {duck.Stock} {duck.Name} in stock.";
        }
        return null;
    }
}

// The store asks every middleware before it reduces an action from a component or an
// effect; the first false completes the dispatch as Vetoed and nothing is reduced.
public sealed class QuantityLimitMiddleware : Middleware
{
    public override bool MayDispatch(ActionContext context) =>
        context.Action is not AddToBasket add
        || ShopRules.RefusalReason(context.State, add.DuckId) is null;
}
