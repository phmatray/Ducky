namespace Ducky.Reactive;

// Pushes, on the drainer (SPEC §14), the state after every processed action when the snapshot changed, then the action
// itself, so a pipeline that reads state when the action arrives (WithLatestFrom, CombineLatest, SelectMany over
// state.Take(1)) sees the state after that action, as ReactiveHost does for StoreInitialized. An unchanged state keeps its
// instance, so a store where nothing changed before StoreInitialized leaves the property null here: ReactiveHost writes
// the first value.
internal sealed class ReactiveMiddleware(ReactiveBridge bridge) : Middleware
{
    public override void AfterReduce(ActionContext context)
    {
        if (!ReferenceEquals(context.State, context.PreviousState))
        {
            bridge.State.Value = context.State;
        }

        bridge.Actions.OnNext(context.Action);
    }
}
