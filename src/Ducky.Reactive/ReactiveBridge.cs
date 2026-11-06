using R3;

namespace Ducky.Reactive;

// One per store (registered Scoped, SPEC §14): its only consumers, ReactiveMiddleware and ReactiveHost, are created from
// the store scope. Values have a single writer, the drainer (INV-01); subscription changes come from timer, pool and
// disposing threads, so State is R3's BehaviorSubject, which locks OnNext and Subscribe (replay and registration), and
// R3's Subject locks Subscribe and subscription disposal.
internal sealed class ReactiveBridge
{
    public Subject<object> Actions { get; } = new();

    // Null only until ReactiveHost writes the current snapshot, before any pipeline subscribes. Pipelines never see its
    // replayed value: ReactivePipeline starts each subscription at the live snapshot, read under the subject's lock.
    // Not R3's SynchronizedReactiveProperty: ReactiveProperty<T>'s [JsonConverter] keeps R3's
    // ReactivePropertyJsonConverterFactory in a trimmed app, whose IL3050/IL2055/IL2072 fail AotSmoke (§10, §14).
    public BehaviorSubject<StateSnapshot> State { get; } = new(null!);

    // Set by ReactiveMiddleware's constructor: the store creates its middleware before it materializes ReactiveHost, the
    // only reader, which publishes reactive failures through it (DispatchSystem is protected).
    public ReactiveMiddleware Middleware { get; set; } = null!;

    // Held by ReactivePipeline from its disposed-slot check until Handle's subscription is made, and taken by ReactiveHost's
    // dispose before it disposes the effects, so a retry in flight when the store is disposed finishes first (§14, INV-27).
    public Lock Gate { get; } = new();
}
