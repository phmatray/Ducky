// Slices, states and selector definitions for SelectorDefTests (SPEC §5.9, §6.9).
namespace Ducky.Tests.SelectorFixtures;

internal sealed record A(int Value);

internal sealed record B(int Value);

internal sealed record C(int Value);

internal sealed record D(int Value);

// Read by no definition: a commit to it changes no input.
internal sealed record E(int Value);

internal sealed record SetA(int Value);

internal sealed record SetE(int Value);

// A fresh instance per projection, so a test can tell a cached result from a recomputed one.
internal sealed record Sum(int Total);

internal sealed class ASlice : Slice<A>
{
    public ASlice() => On<SetA>((_, set) => new A(set.Value));

    protected override A Initial => new(0);
}

internal sealed class BSlice : Slice<B>
{
    protected override B Initial => new(0);
}

internal sealed class CSlice : Slice<C>
{
    protected override C Initial => new(0);
}

internal sealed class DSlice : Slice<D>
{
    protected override D Initial => new(0);
}

internal sealed class ESlice : Slice<E>
{
    public ESlice() => On<SetE>((_, set) => new E(set.Value));

    protected override E Initial => new(0);
}

// Counts projector runs. Projectors must be pure; counting is test-only instrumentation.
internal sealed class Calls
{
    public int Count { get; private set; }

    public Sum Hit(int total)
    {
        Count++;
        return new Sum(total);
    }
}

// Every Selector.Define overload, over the first Arity slots (A, B, C, D) by reference, projecting to a fresh Sum.
internal static class Defs
{
    public static SelectorDef<Sum> Plain(int arity, Calls calls) => arity switch
    {
        1 => Selector.Define(s => s.Get<A>(), a => calls.Hit(a.Value)),
        2 => Selector.Define(s => s.Get<A>(), s => s.Get<B>(), (a, b) => calls.Hit(a.Value + (b.Value * 10))),
        3 => Selector.Define(
            s => s.Get<A>(), s => s.Get<B>(), s => s.Get<C>(), (a, b, c) => calls.Hit(a.Value + (b.Value * 10) + (c.Value * 100))),
        _ => Selector.Define(
            s => s.Get<A>(),
            s => s.Get<B>(),
            s => s.Get<C>(),
            s => s.Get<D>(),
            (a, b, c, d) => calls.Hit(a.Value + (b.Value * 10) + (c.Value * 100) + (d.Value * 1000))),
    };

    public static SelectorDef<int, Sum> WithArg(int arity, Calls calls) => arity switch
    {
        1 => Selector.Define(s => s.Get<A>(), (A a, int arg) => calls.Hit(a.Value + (arg * 10000))),
        2 => Selector.Define(
            s => s.Get<A>(), s => s.Get<B>(), (A a, B b, int arg) => calls.Hit(a.Value + (b.Value * 10) + (arg * 10000))),
        3 => Selector.Define(
            s => s.Get<A>(),
            s => s.Get<B>(),
            s => s.Get<C>(),
            (A a, B b, C c, int arg) => calls.Hit(a.Value + (b.Value * 10) + (c.Value * 100) + (arg * 10000))),
        _ => Selector.Define(
            s => s.Get<A>(),
            s => s.Get<B>(),
            s => s.Get<C>(),
            s => s.Get<D>(),
            (A a, B b, C c, D d, int arg) => calls.Hit(a.Value + (b.Value * 10) + (c.Value * 100) + (d.Value * 1000) + (arg * 10000))),
    };
}
