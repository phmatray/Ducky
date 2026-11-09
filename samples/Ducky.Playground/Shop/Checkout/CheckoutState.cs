// A failing effect does not stop the store: it dispatches EffectFailed, naming the
// action that started the run, and the checkout slice turns it into a message.
using System.Collections.Immutable;
using Ducky.Playground.Shop.Basket;
using Ducky.Playground.Shop.Catalogue;
using Ducky.Playground.Shop.Orders;

namespace Ducky.Playground.Shop.Checkout;

public enum CheckoutPhase
{
    Idle,
    Paying,
    Declined,
}

// Requests counts every CheckOut the store reduced; Payments counts the ones the Exhaust
// effect turned into a payment. The difference is what Exhaust dropped.
public sealed record CheckoutState(
    CheckoutPhase Phase,
    string? Problem,
    bool FailNextPayment,
    int Requests,
    int Payments);

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
        On<PaymentStarted>(state => state with
        {
            Phase = CheckoutPhase.Paying,
            Problem = null,
            FailNextPayment = false,
            Payments = state.Payments + 1,
        });
        On<OrderPlaced>(state => state with { Phase = CheckoutPhase.Idle });
        On<EffectFailed>((state, failed) => failed.ActionType == CheckOut.Type
            ? state with
            {
                Phase = CheckoutPhase.Declined,
                Problem = failed.Exception.Message,
            }
            : state);
        On<RigNextPayment>((state, action) =>
            state with { FailNextPayment = action.Fail });
        On<DismissCheckoutProblem>(state => state.Phase == CheckoutPhase.Declined
            ? state with { Phase = CheckoutPhase.Idle, Problem = null }
            : state);
    }

    public override string Key => "checkout";

    protected override CheckoutState Initial => new(
        CheckoutPhase.Idle,
        Problem: null,
        FailNextPayment: false,
        Requests: 0,
        Payments: 0);
}

public static class Pricing
{
    // One priced line per duck still in the catalogue, in basket order.
    public static ImmutableList<OrderLine> PriceBasket(
        BasketState basket,
        CatalogueState catalogue) =>
    [
        .. basket.Lines
            .Select(line => (Line: line, Duck: catalogue.Ducks.Find(line.DuckId)))
            .Where(static pair => pair.Duck is not null)
            .Select(static pair => new OrderLine(
                pair.Line.DuckId,
                pair.Duck!.Name,
                pair.Line.Quantity,
                pair.Duck.Price)),
    ];

    // Memoized: rebuilt only when the basket or the catalogue changed.
    public static readonly SelectorDef<ImmutableList<OrderLine>> PricedBasket =
        Selector.Define(
            static state => state.Get<BasketState>(),
            static state => state.Get<CatalogueState>(),
            static (basket, catalogue) => PriceBasket(basket, catalogue));
}
