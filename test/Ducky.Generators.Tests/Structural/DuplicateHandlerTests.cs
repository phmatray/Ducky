using Ducky.Generators.Analyzers.Structural;
using static Ducky.Generators.Tests.Structural.StructuralSources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.StructuralAnalyzer>;

namespace Ducky.Generators.Tests.Structural;

// DUCKY014 (SPEC §16.2, ADR-0008): a second On<T> for the same T in one slice or effect-group constructor. Reported on
// every repeated call after the first; the argument is T.
public sealed class DuplicateHandlerTests
{
    public static TheoryData<string, int, string> Reported => new()
    {
        { Slice("On<Hit>((s, a) => s); {|#0:On<Hit>|}((s, a) => s);"), 1, "Hit" },
        { Slice("On<Hit>((s, a) => s); On<Miss>(s => s); {|#0:On<Hit>|}(s => s);"), 1, "Hit" },
        { Slice("On<Hit>(s => s); {|#0:On<Hit>|}(s => s); {|#1:this.On<Hit>|}(s => s);"), 2, "Hit" },
        { Group("On<Hit>((a, c, t) => Task.CompletedTask); {|#0:On<Hit>|}((a, c, t) => Task.CompletedTask, Concurrency.Switch);"), 1, "Hit" },
        // Tuple element names are erased at run time: both are ValueTuple<int, int>.
        { Slice("On<(int a, int b)>(s => s); {|#0:On<(int x, int y)>|}(s => s);"), 1, "(int x, int y)" },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky014_DuplicateOn_Reported(string source, int count, string type) =>
        Verifier.VerifyAsync(
            source,
            [.. Enumerable.Range(0, count).Select(i => Verifier.Diagnostic(StructuralDiagnostics.DuplicateHandler).WithLocation(i).WithArguments(type))]);

    public static TheoryData<string> Silent => new()
    {
        Slice("On<Hit>((s, a) => s); On<Miss>((s, a) => s);"),
        // One registration per constructor: each construction registers once.
        Prelude + "public sealed class Two : Slice<S> { protected override S Initial => new(0); public Two() { On<Hit>(s => s); } public Two(int x) { On<Hit>(s => s); } }",
        // Outside a constructor On<T> throws at run time for another reason (the slice is frozen).
        Prelude + "public sealed class Late : Slice<S> { protected override S Initial => new(0); public void Again() { On<Hit>(s => s); On<Hit>(s => s); } }",
        // Two slices each register Hit once.
        Slice("On<Hit>(s => s);") + "public sealed class Other : Slice<T2> { protected override T2 Initial => new(0); public Other() { On<Hit>(s => s); } }",
    };

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Ducky014_DistinctOn_NotReported(string source) => Verifier.VerifyAsync(source);
}
