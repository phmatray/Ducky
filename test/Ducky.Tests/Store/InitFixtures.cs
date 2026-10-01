// Slices and actions for InitTests (SPEC §6.3, §6.7). Reducers must be pure; these probes are not, on purpose.
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

// Stands in for middleware init (M4-03): the store's init stays Running until Release, and Started counts the calls.
internal sealed class InitGate
{
    private readonly TaskCompletionSource _gate = new();

    public int Started { get; private set; }

    public Func<Task> Init => () =>
    {
        Started++;
        return _gate.Task;
    };

    // Completes inline, so the init continuation (Complete, then MarkReady) runs on the caller.
    public void Release() => _gate.SetResult();
}
