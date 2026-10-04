namespace Ducky.Blazor.Tests.Core;

internal sealed record Increment;

internal sealed record Counter(int Value);

internal sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice() => On<Increment>(state => state with { Value = state.Value + 1 });

    protected override Counter Initial => new(0);
}

// A [JSInvokable] target, as a DotNetObjectReference wraps one.
internal sealed class InteropTarget;
