using Ducky;

namespace PackageSmoke;

// SPEC §17.9 step 1 (stage 7): linked into the WASM, Server and Ducky.Blazor-only consumers, so each compiles its own
// generated Increment dispatch helper (GEN-01) and its AddDuckyGenerated_{Asm} adds CounterSlice (GEN-02) from packages.
[DuckyAction]
internal sealed record Increment(int By = 1);

internal sealed record Counter(int Value);

internal sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice() => On<Increment>((state, action) => state with { Value = state.Value + action.By });

    protected override Counter Initial => new(0);
}
