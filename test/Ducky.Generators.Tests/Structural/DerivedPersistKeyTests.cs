using Ducky.Generators.Analyzers.Structural;
using Microsoft.CodeAnalysis.Testing;
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

    // The Persist<T>() arm (stage 12, P10): a Persist<TSlice> call of Ducky.Blazor on a slice that doesn't pin its Key,
    // reported on the call, whatever its syntax.
    private const string Slices = """
        public sealed class CartSlice : Slice<S> { protected override S Initial => new(0); }
        public sealed class CartStore : SliceStore<T2> { protected override T2 Initial => new(0); }
        public sealed class PinnedSlice : Slice<Hit> { public override string Key => "pinned"; protected override Hit Initial => new(0); }

        """;

    public static TheoryData<string, string> CallReported => new()
    {
        { "{|#0:d.Persist<CartSlice>()|};", "CartSlice" },
        { "{|#0:d.Persist<CartStore>(static o => o.Version = 2)|};", "CartStore" },
        { "{|#0:DuckyBlazorBuilderExtensions.Persist<CartSlice>(d)|};", "CartSlice" },
        { "{|#0:d.AddSlice<CartSlice>().Persist<CartSlice>()|}.Persist<PinnedSlice>();", "CartSlice" },
    };

    [Theory]
    [MemberData(nameof(CallReported))]
    public Task Ducky012_PersistCallWithDerivedKey_Reported(string call, string slice) =>
        Verifier.VerifyAsync(Blazor + Prelude + Slices + Configure(call), Verifier.Diagnostic(StructuralDiagnostics.DerivedPersistKey).WithLocation(0).WithArguments(slice));

    public static TheoryData<string> CallSilent => new()
    {
        "d.Persist<PinnedSlice>();",
        // Other Ducky.Blazor calls and a user's own Persist<T> are not the arm's.
        "d.Prerender<CartSlice>();",
        "d.PersistAttributeDefaults<CartSlice>(static _ => { });",
        "Mine.Persist<CartSlice>(d);",
        // A type parameter has no Key of its own to pin.
        "Generic<CartSlice>(d); static void Generic<T>(DuckyBuilder b) where T : Slice, new() => b.Persist<T>();",
    };

    [Theory]
    [MemberData(nameof(CallSilent))]
    public Task Ducky012_PersistCallPinnedOrOther_NotReported(string call) =>
        Verifier.VerifyAsync(Blazor + Prelude + Slices + "public static class Mine { public static DuckyBuilder Persist<T>(DuckyBuilder b) => b; }\n" + Configure(call));

    // A type argument outside Slice<T> (CS0311) or one that doesn't resolve (CS0246): the analyzer neither crashes nor
    // adds DUCKY012.
    [Theory]
    [InlineData("d.{|#0:Persist<object>|}();", "CS0311")]
    [InlineData("d.Persist<{|#0:Missing|}>();", "CS0246")]
    public Task Ducky012_PersistCallInvalidTypeArgument_OnlyCompilerError(string call, string error) =>
        Verifier.VerifyAsync(Blazor + Prelude + Slices + Configure(call), DiagnosticResult.CompilerError(error).WithLocation(0));

    private const string Blazor = "using Ducky.Blazor;\n";

    private static string Configure(string call) => $$"""
        public static class Setup
        {
            public static void Configure(DuckyBuilder d)
            {
                {{call}}
            }
        }
        """;
}
