// Slices and actions for DispatcherTests (SPEC §6.3, §6.4). Reducers must be pure; these probes are not, on purpose.
namespace Ducky.Tests.DispatcherFixtures;

internal sealed record Count(int Value);

internal sealed record Bump;

internal sealed record Boom;

internal sealed record Ping;

internal sealed record Probe;

// Bump adds 1 and Boom adds 100; Probe runs OnProbe, so a test can call into the store from inside a reducer.
internal sealed class CountSlice : Slice<Count>
{
    public CountSlice()
    {
        On<Bump>(state =>
        {
            Reduced.Add(nameof(Bump));
            return state with { Value = state.Value + 1 };
        });
        On<Boom>(state => state with { Value = state.Value + 100 });
        On<Probe>(state =>
        {
            Reduced.Add($"{nameof(Probe)} start");
            OnProbe?.Invoke();
            Reduced.Add($"{nameof(Probe)} end");
            return state;
        });
    }

    public List<string> Reduced { get; } = [];

    public Action? OnProbe { get; set; }

    protected override Count Initial => new(0);
}

internal sealed record Flag(bool On);

// Registered after CountSlice: its Boom reducer throws after CountSlice already reduced Boom. Ping is its own action.
internal sealed class BoomSlice : Slice<Flag>
{
    public BoomSlice()
    {
        On<Boom>(_ => throw new InvalidOperationException("boom"));
        On<Ping>(state => state with { On = !state.On });
    }

    // Set after the store is built: from then on the dispatcher must use the key the Registry cached, never Key itself.
    public bool KeyThrows { get; set; }

    public override string Key => KeyThrows ? throw new InvalidOperationException(nameof(Key)) : base.Key;

    protected override Flag Initial => new(false);
}

internal sealed record Step(string Name);

// Step runs OnStep with the action, so a test can observe the causal scope and dispatch from inside a reducer.
internal sealed class StepSlice : Slice<Count>
{
    public StepSlice() => On<Step>((state, step) =>
    {
        OnStep?.Invoke(step);
        return state;
    });

    public Action<Step>? OnStep { get; set; }

    protected override Count Initial => new(0);
}
