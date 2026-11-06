using Ducky;

namespace Ducky1x.After;

// Ducky 2.0: any record is a state; its slice declares the initial value.
public sealed record CounterState(int Value);

public sealed class CounterSlice : Slice<CounterState>
{
    protected override CounterState Initial => new(0);
}
