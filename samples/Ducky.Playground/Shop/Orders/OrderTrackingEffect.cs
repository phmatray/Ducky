using Ducky.Playground.Shop.Api;
using Ducky.Reactive;
using R3;

namespace Ducky.Playground.Shop.Orders;

// A Ducky.Reactive pipeline: the state stream drives the timer. While an order is on its way, poll its status every
// Interval on the injected TimeProvider; when it is delivered (or dismissed) Tracking turns null and Switch disposes the
// timer, so polling stops by itself. A reload restores the order and the same stream starts polling again.
public sealed class OrderTrackingEffect(ShopApi api, TimeProvider time) : ReactiveEffect
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1.5);

    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        state.Select(static s => s.Get<OrdersState>().Tracking)
            .DistinctUntilChanged()
            .Select(orderId => orderId is null
                ? Observable.Empty<object>()
                : Observable.Interval(Interval, time)
                    .SelectAwait(
                        async (_, token) => (object)new OrderStatusPolled(orderId, await api.GetOrderStatusAsync(orderId, token)),
                        AwaitOperation.Drop)) // a tick while a request is in flight is skipped
            .Switch();
}
