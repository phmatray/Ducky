namespace Ducky.Reactive;

// Pushes, on the drainer (SPEC §14), the state after every processed action when the snapshot changed, then the action
// itself, so a pipeline that reads state when the action arrives (WithLatestFrom, CombineLatest, SelectMany over
// state.Take(1)) sees the state after that action, as ReactiveHost does for StoreInitialized. An unchanged state keeps its
// instance, so a store where nothing changed before StoreInitialized leaves the property null here: ReactiveHost writes
// the first value.
internal sealed class ReactiveMiddleware : Middleware
{
    private readonly ReactiveBridge _bridge;

    public ReactiveMiddleware(ReactiveBridge bridge)
    {
        _bridge = bridge;
        bridge.Middleware = this;
    }

    // SPEC §14: a System failure action, never vetoed, never buffered, never "unhandled", and under INV-12: DispatchSystem
    // keeps one raised synchronously under a failure action from being dispatched. The caller has logged the exception.
    public void PublishFailure(Type effectType, Exception exception) =>
        DispatchSystem(new EffectFailed(effectType.ToString(), "reactive", exception), isFailure: true);

    public override void AfterReduce(ActionContext context)
    {
        if (!ReferenceEquals(context.State, context.PreviousState))
        {
            _bridge.State.Value = context.State;
        }

        _bridge.Actions.OnNext(context.Action);
    }
}
