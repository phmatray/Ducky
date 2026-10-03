namespace Ducky.Concurrency.Tests;

// A middleware whose init and hooks are delegates (SPEC §5.6). Hooks run on the drainer; Init's synchronous part runs on
// whichever thread starts init.
internal sealed class HookProbe : Middleware
{
    public Func<CancellationToken, ValueTask>? Init { get; init; }

    public Action<ActionContext>? Before { get; init; }

    public Action<ActionContext>? After { get; init; }

    public Action? OnDispose { get; init; }

    public override ValueTask InitializeAsync(CancellationToken cancellationToken) =>
        Init?.Invoke(cancellationToken) ?? ValueTask.CompletedTask;

    public override void BeforeReduce(ActionContext context) => Before?.Invoke(context);

    public override void AfterReduce(ActionContext context) => After?.Invoke(context);

    public override ValueTask DisposeAsync()
    {
        OnDispose?.Invoke();
        return base.DisposeAsync();
    }
}

internal sealed record Boot(int Hydrated, bool Initialized);

// Restored by hydration, then marked by StoreInitialized: both committed is what InitializeAsync promises (§6.7).
internal sealed class BootSlice : Slice<Boot>
{
    public BootSlice() => On<StoreInitialized>(state => state with { Initialized = true });

    protected override Boot Initial => new(0, false);
}
