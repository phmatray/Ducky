using BenchmarkDotNet.Attributes;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Benchmarks;

public sealed record Counter(int Value);

public sealed record Increment(int By);

public sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice() => On<Increment>((state, action) => state with { Value = state.Value + action.By });

    protected override Counter Initial => new(0);
}

// One reduced dispatch through the real store, alone and with selections whose onChange it notifies.
[MemoryDiagnoser]
public class DispatchBenchmarks
{
    private readonly Increment _increment = new(1);
    private ServiceProvider _services = null!;
    private IStore _store = null!;
    private List<Selection<int>> _selections = [];

    [Params(0, 100)]
    public int Selections { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _services = new ServiceCollection().AddDucky(d => d.AddDuckyGenerated_Ducky_Benchmarks()).BuildServiceProvider();
        _store = _services.GetRequiredService<IStore>();
        await _store.InitializeAsync().ConfigureAwait(false);
        _selections = [.. Enumerable.Range(0, Selections).Select(i => _store.Select(state => state.Get<Counter>().Value + i, onChange: _ => { }))];
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        _selections.ForEach(s => s.Dispose());
        await _services.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public Task<DispatchResult> Dispatch() => _store.DispatchAsync(_increment);
}
