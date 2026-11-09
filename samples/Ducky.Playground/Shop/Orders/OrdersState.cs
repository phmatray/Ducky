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

public sealed record OrderLine(string DuckId, string Name, int Quantity, decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
}

public sealed record Order(string Id, DateTimeOffset PlacedAt, ImmutableList<OrderLine> Lines, decimal Total, string DeliverTo, OrderStatus Status, int Polls);

public sealed record OrdersState(Order? Current, int Delivered)
{
    // The order the poller follows: set while the current order is on its way, null once it is delivered.
    public string? Tracking => Current is { Status: not OrderStatus.Delivered } order ? order.Id : null;
}

[ActionType("orders/placed")]
public sealed record OrderPlaced(string OrderId, DateTimeOffset PlacedAt, ImmutableList<OrderLine> Lines, decimal Total, string DeliverTo);

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
            Current = new Order(action.OrderId, action.PlacedAt, action.Lines, action.Total, action.DeliverTo, OrderStatus.Placed, Polls: 0),
        });
        On<OrderStatusPolled>((state, action) => state.Current is { } order && order.Id == action.OrderId
            ? state with
            {
                Current = order with { Status = action.Status > order.Status ? action.Status : order.Status, Polls = order.Polls + 1 },
                Delivered = state.Delivered + (action.Status == OrderStatus.Delivered && order.Status != OrderStatus.Delivered ? 1 : 0),
            }
            : state);
        On<DismissOrder>(state => state.Current is null ? state : state with { Current = null });
    }

    // Persisted and synced like the basket: a reload, or another tab, picks the tracking up where it was.
    public override string Key => "orders";

    protected override OrdersState Initial => new(Current: null, Delivered: 0);
}
