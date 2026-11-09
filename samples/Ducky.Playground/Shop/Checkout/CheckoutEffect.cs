// Checkout on an Exhaust effect: while a payment runs, every further CheckOut is
// dropped, so a double or triple click pays once.
using Ducky.Playground.Shop.Account;
using Ducky.Playground.Shop.Api;
using Ducky.Playground.Shop.Basket;
using Ducky.Playground.Shop.Catalogue;
using Ducky.Playground.Shop.Orders;

namespace Ducky.Playground.Shop.Checkout;

public sealed class CheckoutEffect(ShopApi api) : Effect<CheckOut>
{
    // The pay button stays enabled on purpose: this policy is what protects the order.
    // The store logs each drop and counts it on ducky.effect.dropped.
    public override Concurrency Policy => Concurrency.Exhaust;

    public override async Task Handle(
        CheckOut action,
        EffectContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var basket = state.Get<BasketState>();
        var lines = Pricing.PriceBasket(basket, state.Get<CatalogueState>());
        if (lines.IsEmpty)
        {
            return;
        }

        var total = lines.Sum(static line => line.Total);
        var rigged = state.Get<CheckoutState>().FailNextPayment;
        var deliverTo = state.Get<Profile>().DeliverTo;
        context.Dispatch(new PaymentStarted(total, rigged));

        // A declined payment throws; the store turns the exception into EffectFailed.
        var receipt = await api.PayAsync(lines, total, rigged, cancellationToken);
        context.Dispatch(
            new OrderPlaced(receipt.OrderId, receipt.PlacedAt, lines, total, deliverTo));
    }
}
