using CsCheck;
using Ducky.Tests.SelectorFixtures;
using Ducky.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §5.9 (SEL-01), §6.9; INV-21, ADR-0007.
public sealed class SelectorDefTests
{
    private static readonly int[] _arities = [1, 2, 3, 4];

    // Shared by both stores of TwoStores_ShareDefinition_NoSharedCache: a definition holds no cache.
    private static readonly SelectorDef<Sum> _shared = Selector.Define(s => s.Get<A>(), a => new Sum(a.Value));

    [Fact]
    public void Memoized_EqualsUnmemoized()
    {
        // Steps of (slot, delta, arg): delta 0 commits an equal but new instance, which a reference input must not match.
        var steps = Gen.Select(Gen.Int[0, 4], Gen.Int[0, 2], Gen.Int[0, 2]).List[0, 12];
        Property.Check(steps, run =>
        {
            var calls = new Calls();
            var arg = 0;
            var plain = _arities.Select(arity => Defs.Plain(arity, calls)).ToArray();
            var withArg = _arities.Select(arity => Defs.WithArg(arity, calls)).ToArray();
            var memoPlain = plain.Select(def => def.Create()).ToArray();
            var memoArg = withArg.Select(def => def.Create(() => arg)).ToArray();
            var lastPlain = new Sum?[_arities.Length];
            var lastArg = new Sum?[_arities.Length];
            var state = Initial();
            Check(slot: -1, argChanged: false);
            foreach (var (slot, delta, nextArg) in run)
            {
                state = With(state, slot, delta);
                var argChanged = nextArg != arg;
                arg = nextArg;
                Check(slot, argChanged);
            }

            // INV-21 both halves: the value matches the unmemoized one, and the instance is reused
            // exactly when no input reference (nor, for arg memos, the argument) changed.
            void Check(int slot, bool argChanged)
            {
                for (var i = 0; i < _arities.Length; i++)
                {
                    var inputChanged = slot >= 0 && slot < _arities[i];
                    lastPlain[i] = Next(memoPlain[i](state), plain[i].Evaluate(state), lastPlain[i], inputChanged);
                    lastArg[i] = Next(memoArg[i](state), withArg[i].Evaluate(state, arg), lastArg[i], inputChanged || argChanged);
                }
            }

            static Sum Next(Sum result, Sum expected, Sum? last, bool changed)
            {
                result.ShouldBe(expected);
                if (last is not null)
                {
                    if (changed)
                    {
                        result.ShouldNotBeSameAs(last);
                    }
                    else
                    {
                        result.ShouldBeSameAs(last);
                    }
                }

                return result;
            }
        });
    }

    [Fact]
    public void SameInputRefs_SameOutputInstance()
    {
        foreach (var arity in _arities)
        {
            var calls = new Calls();
            var def = Defs.Plain(arity, calls);
            var select = def.Create();
            var state = Initial();

            var first = select(state);
            select(state).ShouldBeSameAs(first);
            select(With(state, slot: 4, delta: 1)).ShouldBeSameAs(first, "a commit to an unread slice changes no input");
            calls.Count.ShouldBe(1);

            // Any input changing reference recomputes, even to an equal record (ADR-0007); the new entry is then cached.
            for (var slot = 0; slot < arity; slot++)
            {
                state = With(state, slot, delta: 0);
                var next = select(state);
                next.ShouldNotBeSameAs(first);
                next.ShouldBe(def.Evaluate(state));
                select(state).ShouldBeSameAs(next);
                first = next;
            }

            calls.Count.ShouldBe(1 + arity + arity);
        }
    }

    // Non-normative: a value-type input matches by EqualityComparer.Default, so an equal value read from a new record
    // keeps the cached result, and a changed value recomputes (SPEC §6.9).
    [Fact]
    public void ValueTypeInput_EqualValue_SameOutputInstance()
    {
        var calls = new Calls();
        var select = Selector.Define(s => s.Get<A>().Value, s => s.Get<B>().Value, (a, b) => calls.Hit(a + b)).Create();
        var state = Initial();

        var first = select(state);
        select(With(state, slot: 0, delta: 0)).ShouldBeSameAs(first);
        select(With(state, slot: 1, delta: 0)).ShouldBeSameAs(first);
        calls.Count.ShouldBe(1);

        select(With(state, slot: 1, delta: 2)).ShouldBe(new Sum(2));
        calls.Count.ShouldBe(2);
    }

    [Fact]
    public void ArgSelector_ArgChange_Recomputes()
    {
        foreach (var arity in _arities)
        {
            var calls = new Calls();
            var def = Defs.WithArg(arity, calls);
            var arg = 1;
            var select = def.Create(() => arg);
            var state = Initial();

            var first = select(state);
            first.ShouldBe(def.Evaluate(state, 1));
            select(state).ShouldBeSameAs(first);
            calls.Count.ShouldBe(2);

            arg = 2;
            var second = select(state);
            second.ShouldBe(def.Evaluate(state, 2));
            select(state).ShouldBeSameAs(second);

            // The inputs still count with an unchanged argument: each changed slot recomputes.
            for (var slot = 0; slot < arity; slot++)
            {
                state = With(state, slot, delta: 0);
                select(state).ShouldNotBeSameAs(second);
                second = select(state);
            }

            calls.Count.ShouldBe(4 + arity);
        }
    }

    // Non-normative: the argument comparer defaults to EqualityComparer<TArg>.Default (value equality, even for a
    // reference type), and a given comparer replaces it.
    [Fact]
    public void ArgSelector_Comparer_DecidesArgMatch()
    {
        var calls = new Calls();
        var def = Selector.Define(s => s.Get<A>(), (A a, string arg) => calls.Hit(a.Value + arg.Length));
        var arg = "ab";
        var byDefault = def.Create(() => arg);
        var ignoringCase = def.Create(() => arg, StringComparer.OrdinalIgnoreCase);
        var state = Initial();

        var first = byDefault(state);
        arg = new string(['a', 'b']);
        byDefault(state).ShouldBeSameAs(first);
        arg = "AB";
        byDefault(state).ShouldNotBeSameAs(first);
        calls.Count.ShouldBe(2);

        var cased = ignoringCase(state);
        arg = "ab";
        ignoringCase(state).ShouldBeSameAs(cased);
        calls.Count.ShouldBe(3);
    }

    [Fact]
    public async Task TwoStores_ShareDefinition_NoSharedCache()
    {
        await using var storeA = new DuckyStore([new ASlice()], NullLogger.Instance);
        await using var storeB = new DuckyStore([new ASlice()], NullLogger.Instance);
        (await storeB.DispatchAsync(new SetA(5))).ShouldBe(DispatchResult.Reduced);
        var selectA = _shared.Create();
        var selectB = _shared.Create();

        var fromA = selectA(storeA.State);
        var fromB = selectB(storeB.State);
        fromA.ShouldBe(new Sum(0));
        fromB.ShouldBe(new Sum(5));

        // Each Create has its own entry: reading B through selectB never evicts A's result from selectA, and selectB
        // computes its own result for A's state rather than reusing selectA's.
        selectA(storeA.State).ShouldBeSameAs(fromA);
        selectB(storeB.State).ShouldBeSameAs(fromB);
        var aThroughB = selectB(storeA.State);
        aThroughB.ShouldBe(fromA);
        aThroughB.ShouldNotBeSameAs(fromA);
        selectA(storeA.State).ShouldBeSameAs(fromA);

        // Evaluate never caches.
        _shared.Evaluate(storeA.State).ShouldNotBeSameAs(_shared.Evaluate(storeA.State));
    }

    // Non-normative: every delegate is checked when the definition is made, not on first use.
    [Fact]
    public void Define_NullDelegate_ThrowsArgumentNull()
    {
        Func<StateSnapshot, int> i = _ => 0;
        Func<StateSnapshot, int>? n = null;
        var cases = new (Action Define, string Param)[]
        {
            (() => Selector.Define(n!, (int _) => 0), "in1"),
            (() => Selector.Define(i, (Func<int, int>)null!), "projector"),
            (() => Selector.Define(n!, i, (int _, int _) => 0), "in1"),
            (() => Selector.Define(i, n!, (int _, int _) => 0), "in2"),
            (() => Selector.Define(i, i, (Func<int, int, int>)null!), "projector"),
            (() => Selector.Define(n!, i, i, (int _, int _, int _) => 0), "in1"),
            (() => Selector.Define(i, n!, i, (int _, int _, int _) => 0), "in2"),
            (() => Selector.Define(i, i, n!, (int _, int _, int _) => 0), "in3"),
            (() => Selector.Define(i, i, i, (Func<int, int, int, int>)null!), "projector"),
            (() => Selector.Define(n!, i, i, i, (int _, int _, int _, int _) => 0), "in1"),
            (() => Selector.Define(i, n!, i, i, (int _, int _, int _, int _) => 0), "in2"),
            (() => Selector.Define(i, i, n!, i, (int _, int _, int _, int _) => 0), "in3"),
            (() => Selector.Define(i, i, i, n!, (int _, int _, int _, int _) => 0), "in4"),
            (() => Selector.Define(i, i, i, i, (Func<int, int, int, int, int>)null!), "projector"),
            (() => Selector.Define(n!, (int _, string _) => 0), "in1"),
            (() => Selector.Define(i, (Func<int, string, int>)null!), "projector"),
            (() => Selector.Define(n!, i, (int _, int _, string _) => 0), "in1"),
            (() => Selector.Define(i, n!, (int _, int _, string _) => 0), "in2"),
            (() => Selector.Define(i, i, (Func<int, int, string, int>)null!), "projector"),
            (() => Selector.Define(n!, i, i, (int _, int _, int _, string _) => 0), "in1"),
            (() => Selector.Define(i, n!, i, (int _, int _, int _, string _) => 0), "in2"),
            (() => Selector.Define(i, i, n!, (int _, int _, int _, string _) => 0), "in3"),
            (() => Selector.Define(i, i, i, (Func<int, int, int, string, int>)null!), "projector"),
            (() => Selector.Define(n!, i, i, i, (int _, int _, int _, int _, string _) => 0), "in1"),
            (() => Selector.Define(i, n!, i, i, (int _, int _, int _, int _, string _) => 0), "in2"),
            (() => Selector.Define(i, i, n!, i, (int _, int _, int _, int _, string _) => 0), "in3"),
            (() => Selector.Define(i, i, i, n!, (int _, int _, int _, int _, string _) => 0), "in4"),
            (() => Selector.Define(i, i, i, i, (Func<int, int, int, int, string, int>)null!), "projector"),
            (() => Selector.Define(i, (int _, string _) => 0).Create(null!), "argument"),
        };

        foreach (var (define, param) in cases)
        {
            Should.Throw<ArgumentNullException>(define).ParamName.ShouldBe(param);
        }
    }

    private static StateSnapshot Initial() =>
        new(new Registry([new ASlice(), new BSlice(), new CSlice(), new DSlice(), new ESlice()]));

    // Commits a new instance of the state at slot (A..E) whose value is delta more.
    private static StateSnapshot With(StateSnapshot state, int slot, int delta)
    {
        object next = slot switch
        {
            0 => new A(state.Get<A>().Value + delta),
            1 => new B(state.Get<B>().Value + delta),
            2 => new C(state.Get<C>().Value + delta),
            3 => new D(state.Get<D>().Value + delta),
            _ => new E(state.Get<E>().Value + delta),
        };
        return state.Commit([(slot, next)], Origin.Local);
    }
}
