using R3;

namespace Ducky.Reactive;

// One per store (registered Scoped, SPEC §14): its only consumers, ReactiveMiddleware and ReactiveHost, are created from
// the store scope. Values have a single writer, the drainer (INV-01); subscription changes come from timer, pool and
// disposing threads, so State is the synchronized property, and R3's Subject locks Subscribe and subscription disposal.
internal sealed class ReactiveBridge
{
    public Subject<object> Actions { get; } = new();

    // Null only until ReactiveHost writes the current snapshot, before any pipeline subscribes.
    public SynchronizedReactiveProperty<StateSnapshot> State { get; } = new(null!);
}
