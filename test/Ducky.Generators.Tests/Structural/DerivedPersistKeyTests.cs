using Ducky.Generators.Analyzers.Structural;
using static Ducky.Generators.Tests.Structural.StructuralSources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.StructuralAnalyzer>;

namespace Ducky.Generators.Tests.Structural;

// DUCKY012, [Persist] arm (SPEC §16.2, ADR-0031): a persisted slice that neither overrides Key nor inherits an override
// is stored under the key derived from its type name, so a rename orphans its data. Reported on the attribute; the
// argument is the slice.
public sealed class DerivedPersistKeyTests
{
    public static TheoryData<string, string> Reported => new()
    {
        { "[{|#0:Persist|}] public sealed class CartSlice : Slice<S> { protected override S Initial => new(0); }", "CartSlice" },
        { "[{|#0:Persist(Version = 2)|}] public sealed class CartSlice : Slice<S> { protected override S Initial => new(0); }", "CartSlice" },
        { "[{|#0:Ducky.PersistAttribute|}] public sealed class CartStore : SliceStore<S> { protected override S Initial => new(0); }", "CartStore" },
        { "public abstract class Base : Slice<S>; [{|#0:Persist|}] public sealed class CartSlice : Base { protected override S Initial => new(0); }", "CartSlice" },
        // Hiding Key with `new` doesn't change the key the store uses.
        { "[{|#0:Persist|}] public sealed class CartSlice : Slice<S> { public new string Key => \"cart\"; protected override S Initial => new(0); }", "CartSlice" },
        { "public static class Outer { [{|#0:Persist|}] public sealed class Cart : Slice<S> { protected override S Initial => new(0); } }", "Outer.Cart" },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky012_PersistWithDerivedKey_Reported(string source, string slice) =>
        Verifier.VerifyAsync(Prelude + source, Verifier.Diagnostic(StructuralDiagnostics.DerivedPersistKey).WithLocation(0).WithArguments(slice));

    public static TheoryData<string> Silent => new()
    {
        "[Persist] public sealed class CartSlice : Slice<S> { public override string Key => \"cart\"; protected override S Initial => new(0); }",
        "public abstract class Pinned : Slice<S> { public override string Key => \"cart\"; } [Persist] public sealed class CartSlice : Pinned { protected override S Initial => new(0); }",
        "public sealed class CartSlice : Slice<S> { protected override S Initial => new(0); }",
        // [Persist] on a non-slice is DUCKY022, not DUCKY012.
        "[Persist] public sealed class NotASlice;",
    };

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Ducky012_PinnedOrNotPersisted_NotReported(string source) => Verifier.VerifyAsync(Prelude + source);
}
