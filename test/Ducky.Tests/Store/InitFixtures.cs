// Slices and actions for InitTests (SPEC §6.3, §6.7). Reducers must be pure; these probes are not, on purpose.
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests.InitFixtures;

internal sealed record Mark(string Name);

// The processing order, as committed state: StoreInitialized appends "init", Mark appends its name.
internal sealed record Trail(IReadOnlyList<string> Steps)
{
    public static readonly Trail Empty = new([]);
}

internal sealed class TrailSlice : Slice<Trail>
{
    public TrailSlice()
    {
        On<StoreInitialized>(state =>
        {
            OnInit?.Invoke();
            return new([.. state.Steps, "init"]);
        });
        On<Mark>((state, mark) => new([.. state.Steps, mark.Name]));
    }

    // Runs inside the StoreInitialized reducer, after the store became Ready.
    public Action? OnInit { get; set; }

    protected override Trail Initial => Trail.Empty;
}

internal sealed record Other(int Value);

internal sealed class OtherSlice : Slice<Other>
{
    protected override Other Initial => new(0);
}

// A middleware whose init stays Running until Release; Started counts the InitializeAsync calls.
internal sealed class InitGate : Middleware
{
    private readonly TaskCompletionSource _gate = new();

    public int Started { get; private set; }

    public CancellationToken Token { get; private set; }

    public override ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        Started++;
        Token = cancellationToken;
        return new(_gate.Task);
    }

    // Completes inline, so the init continuation (Complete, then MarkReady) runs on the caller.
    public void Release() => _gate.SetResult();
}

// A middleware whose InitializeAsync is the delegate the test gives it, with Store exposed for the delegate to use.
internal sealed class InitProbe(Func<InitProbe, CancellationToken, ValueTask> init) : Middleware
{
    public IStore AttachedStore => Store;

    public void System(object action) => DispatchSystem(action);

    public override ValueTask InitializeAsync(CancellationToken cancellationToken) => init(this, cancellationToken);
}

// A FakeTimeProvider that counts the timer callbacks it runs, so a test can tell that a timer never fired.
internal sealed class CountingTimeProvider(FakeTimeProvider inner) : TimeProvider
{
    private int _fired;

    public int Fired => Volatile.Read(ref _fired);

    public override long TimestampFrequency => inner.TimestampFrequency;

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

    public override long GetTimestamp() => inner.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        inner.CreateTimer(
            s =>
            {
                Interlocked.Increment(ref _fired);
                callback(s);
            },
            state,
            dueTime,
            period);
}

// Resolved from DI by Init_HangingMiddleware_TimesOutAndReleasesBuffer, which reads the init token through InitTokens.
internal sealed class InitTokens
{
    public List<CancellationToken> Tokens { get; } = [];
}

// Its init ignores the token and never ends.
internal sealed class Hanging(InitTokens tokens) : Middleware
{
    public override ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        tokens.Tokens.Add(cancellationToken);
        return new(new TaskCompletionSource().Task);
    }
}
