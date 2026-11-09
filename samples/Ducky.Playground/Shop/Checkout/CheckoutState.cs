using System.Collections.Immutable;
using Ducky.Playground.Shop.Cart;
using Ducky.Playground.Shop.Catalogue;
using Ducky.Playground.Shop.Orders;

namespace Ducky.Playground.Shop.Checkout;

public enum CheckoutPhase
{
    Idle,
    Paying,
    Declined,
}

// Requests counts every CheckOut the store reduced; Payments the ones the Exhaust effect turned into a payment.
public sealed record CheckoutState(CheckoutPhase Phase, string? Problem, bool FailNextPayment, int Requests, int Payments);

[DuckyAction]
[ActionType(Type)]
public sealed record CheckOut
{
    public const string Type = "checkout/requested";
}

[ActionType("checkout/payment-started")]
public sealed record PaymentStarted(decimal Amount, bool Rigged);

[DuckyAction]
[ActionType("checkout/next-payment-rigged")]
public sealed record RigNextPayment(bool Fail);

[DuckyAction]
[ActionType("checkout/problem-dismissed")]
public sealed record DismissCheckoutProblem;

public sealed class CheckoutSlice : Slice<CheckoutState>
{
    public CheckoutSlice()
    {
        On<CheckOut>(state => state with { Requests = state.Requests + 1 });
        On<PaymentStarted>(state => state with { Phase = CheckoutPhase.Paying, Problem = null, FailNextPayment = false, Payments = state.Payments + 1 });
        On<OrderPlaced>(state => state with { Phase = CheckoutPhase.Idle });

        // The checkout effect lets a declined payment throw: the store dispatches EffectFailed, naming the action that
        // started the run, and keeps running. This reducer turns it into something the basket can show.
        On<EffectFailed>((state, failed) => failed.ActionType == CheckOut.Type
            ? state with { Phase = CheckoutPhase.Declined, Problem = failed.Exception.Message }
            : state);
        On<RigNextPayment>((state, action) => state with { FailNextPayment = action.Fail });
        On<DismissCheckoutProblem>(state => state.Phase == CheckoutPhase.Declined ? state with { Phase = CheckoutPhase.Idle, Problem = null } : state);
    }

    public override string Key => "checkout";

    protected override CheckoutState Initial => new(CheckoutPhase.Idle, Problem: null, FailNextPayment: false, Requests: 0, Payments: 0);
}

public static class Pricing
{
    // The basket priced against the catalogue: one line per duck still on sale, in basket order.
    public static ImmutableList<OrderLine> Lines(Basket basket, CatalogueState catalogue) =>
    [
        .. basket.Lines
            .Select(line => (line, duck: catalogue.Ducks.Find(line.DuckId)))
            .Where(static pair => pair.duck is not null)
            .Select(static pair => new OrderLine(pair.line.DuckId, pair.duck!.Name, pair.line.Quantity, pair.duck.Price)),
    ];

    public static readonly SelectorDef<ImmutableList<OrderLine>> BasketLines = Selector.Define(
        static s => s.Get<Basket>(),
        static s => s.Get<CatalogueState>(),
        static (basket, catalogue) => Lines(basket, catalogue));
}
