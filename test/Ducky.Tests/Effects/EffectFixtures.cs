// Actions, slices and effects for EffectTests (SPEC §5.5, §6.4 step 11, §6.6, §6.11 phase 5b).
namespace Ducky.Tests.EffectFixtures;

internal sealed record Load(int Id);

internal sealed record Loaded(int Id);

internal record Base;

internal sealed record Derived : Base;

internal sealed record Seen(IReadOnlyList<object> Actions);

// Records every action it reduces, EffectFailed included, in processing order.
internal sealed class SeenSlice : Slice<Seen>
{
    public SeenSlice()
    {
        On<Load>(Add);
        On<Loaded>(Add);
        On<Base>(Add);
        On<Derived>(Add);
        On<EffectFailed>(Add);
    }

    protected override Seen Initial => new([]);

    private static Seen Add<TAction>(Seen state, TAction action)
        where TAction : notnull => new([.. state.Actions, action]);
}

// An effect whose handler is a delegate, registered with AddEffect(instance).
internal sealed class Handler<TAction>(Func<TAction, EffectContext, CancellationToken, Task> handle) : Effect<TAction>
    where TAction : notnull
{
    public override Task Handle(TAction action, EffectContext context, CancellationToken cancellationToken) =>
        handle(action, context, cancellationToken);

    // Exposes the protected default key (§5.5).
    public object? KeyOf(TAction action) => ConcurrencyKey(action);
}

// A Switch effect whose handler is a delegate; without a key function every run shares the one global slot (§5.5, §6.6).
internal sealed class SwitchHandler<TAction>(
    Func<TAction, EffectContext, CancellationToken, Task> handle,
    Func<TAction, object?>? key = null) : Effect<TAction>
    where TAction : notnull
{
    public override Concurrency Policy => Concurrency.Switch;

    public override Task Handle(TAction action, EffectContext context, CancellationToken cancellationToken) =>
        handle(action, context, cancellationToken);

    protected override object? ConcurrencyKey(TAction action) => key?.Invoke(action);
}

// A LongRunning effect whose handler is a delegate: WhenIdleAsync never waits for its runs (§6.6).
internal sealed class LongRunningHandler<TAction>(Func<TAction, EffectContext, CancellationToken, Task> handle) : Effect<TAction>
    where TAction : notnull
{
    public override bool LongRunning => true;

    public override Task Handle(TAction action, EffectContext context, CancellationToken cancellationToken) =>
        handle(action, context, cancellationToken);
}

// Implements both dispose interfaces, so a store that disposed an AddEffect(instance) instance would be caught either way.
internal sealed class DisposableHandler : Effect<Load>, IAsyncDisposable, IDisposable
{
    public List<Load> Handled { get; } = [];

    public bool Disposed { get; private set; }

    public override Task Handle(Load action, EffectContext context, CancellationToken cancellationToken)
    {
        Handled.Add(action);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return default;
    }

    public void Dispose() => Disposed = true;
}

// Shared by the store-created effects below, resolved from DI.
internal sealed class EffectJournal
{
    public List<Type> Constructed { get; } = [];

    public List<(Effect Effect, object Action)> Handled { get; } = [];

    public List<string> Disposed { get; } = [];
}

internal class LoadEffect : Effect<Load>, IDisposable
{
    private readonly EffectJournal _journal;

    public LoadEffect(EffectJournal journal)
    {
        _journal = journal;
        journal.Constructed.Add(GetType());
    }

    public bool Disposed { get; private set; }

    public override Task Handle(Load action, EffectContext context, CancellationToken cancellationToken)
    {
        _journal.Handled.Add((this, action));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Disposed = true;
        _journal.Disposed.Add(nameof(LoadEffect));
    }
}

// A test double registered as AddEffect<LoadEffect>(instance): it takes LoadEffect's one runner (§5.1, §15).
internal sealed class FakeLoadEffect(EffectJournal journal) : LoadEffect(journal);

// Store-owned and async-disposable; its DisposeAsync faults, which must not stop the other disposals.
internal sealed class FaultingDisposeEffect(EffectJournal journal) : Effect<Loaded>, IAsyncDisposable
{
    public override Task Handle(Loaded action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        journal.Disposed.Add(nameof(FaultingDisposeEffect));
        return ValueTask.FromException(new InvalidOperationException("dispose"));
    }
}

// Neither IDisposable nor IAsyncDisposable: disposal has nothing to call.
internal sealed class PlainEffect : Effect<Loaded>
{
    public PlainEffect(EffectJournal journal) => journal.Constructed.Add(GetType());

    public override Task Handle(Loaded action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class ThrowingEffect : Effect<Load>
{
    public ThrowingEffect(EffectJournal journal)
    {
        journal.Constructed.Add(GetType());
        throw new FormatException("ctor");
    }

    public override Task Handle(Load action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

// Records its own disposal in the journal, so a test can order it against the effects' disposal.
internal sealed class JournalMiddleware(EffectJournal journal) : Middleware
{
    public override ValueTask DisposeAsync()
    {
        journal.Disposed.Add(nameof(JournalMiddleware));
        return base.DisposeAsync();
    }
}
