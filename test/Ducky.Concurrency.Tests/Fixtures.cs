using System.Collections.Immutable;

// Slices and actions for the interleaving tests. Reducers must be pure; the probes below are not, on purpose.
namespace Ducky.Concurrency.Tests;

internal sealed record Add(int Producer, int Seq);

internal sealed record Block;

internal sealed record Tick;

internal sealed record Log(ImmutableList<Add> Entries);

// Appends every Add in processing order; OnReduce runs inside each reducer call, OnBlock inside the Block reducer.
internal sealed class LogSlice : Slice<Log>
{
    public LogSlice()
    {
        On<Add>((state, action) =>
        {
            OnReduce?.Invoke();
            return state with { Entries = state.Entries.Add(action) };
        });
        On<Block>(state =>
        {
            OnBlock?.Invoke();
            return state;
        });
    }

    public Action? OnReduce { get; set; }

    public Action? OnBlock { get; set; }

    protected override Log Initial => new([]);
}

internal sealed record Left(long Ticks);

internal sealed record Right(long Ticks);

// Two slices that both change on every Tick: a snapshot holding one from another commit is torn.
internal sealed class LeftSlice : Slice<Left>
{
    public LeftSlice() => On<Tick>(state => state with { Ticks = state.Ticks + 1 });

    protected override Left Initial => new(0);
}

internal sealed class RightSlice : Slice<Right>
{
    public RightSlice() => On<Tick>(state => state with { Ticks = state.Ticks + 1 });

    protected override Right Initial => new(0);
}
