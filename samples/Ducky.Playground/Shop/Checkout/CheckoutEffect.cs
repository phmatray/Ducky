using Ducky.Playground.Shop.Account;
using Ducky.Playground.Shop.Api;
using Ducky.Playground.Shop.Cart;
using Ducky.Playground.Shop.Catalogue;
using Ducky.Playground.Shop.Orders;

namespace Ducky.Playground.Shop.Checkout;

// Exhaust: while a payment runs, every further CheckOut is dropped (the store logs it and counts ducky.effect.dropped),
// so a double or triple click pays once. The button stays enabled on purpose: the policy is what protects the order.
public sealed class CheckoutEffect(ShopApi api) : Effect<CheckOut>
{
    public override Concurrency Policy => Concurrency.Exhaust;

    public override async Task Handle(CheckOut action, EffectContext context, CancellationToken cancellationToken)
    {
        var state = context.State;
        var lines = Pricing.Lines(state.Get<Basket>(), state.Get<CatalogueState>());
        if (lines.IsEmpty)
        {
            return;
        }

        var total = lines.Sum(static line => line.Total);
        var rigged = state.Get<CheckoutState>().FailNextPayment;
        var deliverTo = state.Get<Profile>().DeliverTo;
        context.Dispatch(new PaymentStarted(total, rigged));

        // A declined payment throws PaymentDeclinedException out of the handler: the store turns it into EffectFailed.
        var receipt = await api.PayAsync(lines, total, rigged, cancellationToken);
        context.Dispatch(new OrderPlaced(receipt.OrderId, receipt.PlacedAt, lines, total, deliverTo));
    }
}
