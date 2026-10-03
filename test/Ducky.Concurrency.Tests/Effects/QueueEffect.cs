using Ducky;

// Outside the Ducky.Concurrency.Tests namespace, whose Ducky.Concurrency part hides the Ducky.Concurrency enum (CS0435).
namespace ConcurrencyFixtures;

// A Queue effect whose key function and handler are delegates (SPEC §5.5, §6.6).
internal sealed class QueueEffect<TAction>(Func<TAction, object?> key, Func<TAction, Task> handle) : Effect<TAction>
    where TAction : notnull
{
    public override Concurrency Policy => Concurrency.Queue;

    protected override object? ConcurrencyKey(TAction action) => key(action);

    public override Task Handle(TAction action, EffectContext context, CancellationToken cancellationToken) => handle(action);
}
