// The orders slice is persisted and synced across tabs. Its Tracking value drives the
// reactive poller: the order id while it is on its way, null once it is delivered.
using System.Collections.Immutable;

namespace Ducky.Playground.Shop.Orders;

public enum OrderStatus
{
    Placed,
    Packed,
    Shipped,
    OutForDelivery,
    Delivered,
}

public sealed record OrderLine(
    string DuckId,
    string Name,
    int Quantity,
    decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
}

public sealed record Order(
    string Id,
    DateTimeOffset PlacedAt,
    ImmutableList<OrderLine> Lines,
    decimal Total,
    string DeliverTo,
    OrderStatus Status,
    int Polls);

// Delivered counts the orders delivered so far.
public sealed record OrdersState(Order? Current, int Delivered)
{
    public string? Tracking => Current is { Status: not OrderStatus.Delivered } order
        ? order.Id
        : null;
}

[ActionType("orders/placed")]
public sealed record OrderPlaced(
    string OrderId,
    DateTimeOffset PlacedAt,
    ImmutableList<OrderLine> Lines,
    decimal Total,
    string DeliverTo);

[ActionType("orders/status-polled")]
public sealed record OrderStatusPolled(string OrderId, OrderStatus Status);

[DuckyAction]
[ActionType("orders/dismissed")]
public sealed record DismissOrder;

public sealed class OrdersSlice : Slice<OrdersState>
{
    public OrdersSlice()
    {
        On<OrderPlaced>((state, action) => state with
        {
            Current = new Order(
                action.OrderId,
                action.PlacedAt,
                action.Lines,
                action.Total,
                action.DeliverTo,
                OrderStatus.Placed,
                Polls: 0),
        });
        On<OrderStatusPolled>(ApplyPoll);
        On<DismissOrder>(state =>
            state.Current is null ? state : state with { Current = null });
    }

    public override string Key => "orders";

    protected override OrdersState Initial => new(Current: null, Delivered: 0);

    // Answers can arrive out of order, so a late one never moves the order back a step.
    private static OrdersState ApplyPoll(OrdersState state, OrderStatusPolled poll)
    {
        if (state.Current is not { } order || order.Id != poll.OrderId)
        {
            return state;
        }

        var justDelivered = poll.Status == OrderStatus.Delivered
            && order.Status != OrderStatus.Delivered;
        return state with
        {
            Current = order with
            {
                Status = poll.Status > order.Status ? poll.Status : order.Status,
                Polls = order.Polls + 1,
            },
            Delivered = justDelivered ? state.Delivered + 1 : state.Delivered,
        };
    }
}
