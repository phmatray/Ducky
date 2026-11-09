using Ducky.Playground.Shop.Catalogue;

namespace Ducky.Playground.Shop.Cart;

// The shop's rules for a basket, read by the veto middleware and by the components that explain a veto.
public static class ShopRules
{
    public const int PerDuck = 10;

    // Why one more of this duck can't go in the basket, or null when it can.
    public static string? WhyNot(StateSnapshot state, string duckId)
    {
        var inBasket = state.Get<Basket>().QuantityOf(duckId);
        var duck = state.Get<CatalogueState>().Ducks.Find(duckId);
        if (inBasket >= PerDuck)
        {
            return $"{PerDuck} of one duck is our limit.";
        }
        if (duck is not null && inBasket >= duck.Stock)
        {
            return duck.Stock == 0 ? $"{duck.Name} is sold out." : $"Only {duck.Stock} {duck.Name} in stock.";
        }
        return null;
    }
}

// A MayDispatch veto: the store asks every middleware before it reduces a component's or an effect's action, and the
// first false completes the dispatch with DispatchResult.Vetoed. Nothing is reduced, and the component that awaited the
// dispatch learns why from the same rule.
public sealed class QuantityLimitMiddleware : Middleware
{
    public override bool MayDispatch(ActionContext context) =>
        context.Action is not AddToCart add || ShopRules.WhyNot(context.State, add.DuckId) is null;
}
