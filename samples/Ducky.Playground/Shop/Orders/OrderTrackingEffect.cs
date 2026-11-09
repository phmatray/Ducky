// A Ducky.Reactive pipeline where the state stream drives the timer: polling starts
// when an order is on its way and stops by itself once it is delivered or dismissed.
using Ducky.Playground.Shop.Api;
using Ducky.Reactive;
using R3;

namespace Ducky.Playground.Shop.Orders;

public sealed class OrderTrackingEffect(ShopApi api, TimeProvider time) : ReactiveEffect
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1.5);

    // When Tracking turns null, Switch disposes the running timer. After a reload, the
    // restored order starts the same stream polling again.
    public override Observable<object> Handle(
        Observable<object> actions,
        Observable<StateSnapshot> state) =>
        state.Select(static snapshot => snapshot.Get<OrdersState>().Tracking)
            .DistinctUntilChanged()
            .Select(orderId => orderId is null
                ? Observable.Empty<object>()
                : Observable.Interval(Interval, time).SelectAwait(
                    (_, token) => PollAsync(orderId, token),
                    // A tick that arrives while a request is in flight is skipped.
                    AwaitOperation.Drop))
            .Switch();

    private async ValueTask<object> PollAsync(string orderId, CancellationToken token) =>
        new OrderStatusPolled(orderId, await api.GetOrderStatusAsync(orderId, token));
}
