using Ducky.Generators.Analyzers.Structural;
using static Ducky.Generators.Tests.Structural.StructuralSources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.StructuralAnalyzer>;

namespace Ducky.Generators.Tests.Structural;

// DUCKY015 (SPEC §16.2, ADR-0009): each state type maps to exactly one slice, so every non-abstract slice whose state
// type another non-abstract slice of the compilation also has is reported on its name, with the state and the other
// slices (ordinal order) as arguments.
public sealed class SharedStateTests
{
    private static string SliceOf(string name, string state, string modifiers = "sealed") =>
        $"public {modifiers} class {name} : Slice<{state}> {{ protected override {state} Initial => new(0); }}\n";

    private static Microsoft.CodeAnalysis.Testing.DiagnosticResult At(int location, string slice, string state, string others) =>
        Verifier.Diagnostic(StructuralDiagnostics.SharedState).WithLocation(location).WithArguments(slice, state, others);

    [Fact]
    public Task Ducky015_TwoSlicesShareState_BothReported() =>
        Verifier.VerifyAsync(
            Prelude + SliceOf("{|#0:A|}", "S") + SliceOf("{|#1:B|}", "S") + SliceOf("C", "T2"),
            At(0, "A", "S", "B"),
            At(1, "B", "S", "A"));

    [Fact]
    public Task Ducky015_ThreeSlicesShareState_OthersInOrdinalOrder() =>
        Verifier.VerifyAsync(
            Prelude + SliceOf("{|#0:B|}", "S") + SliceOf("{|#1:C|}", "S") + SliceOf("{|#2:A|}", "S"),
            At(0, "B", "S", "A', 'C"),
            At(1, "C", "S", "A', 'B"),
            At(2, "A", "S", "B', 'C"));

    [Fact]
    public Task Ducky015_SliceStoreAndDerivedSliceAcrossFiles_Reported()
    {
        var test = Verifier.Test(
            Prelude + "public class {|#0:A|} : Slice<S> { protected override S Initial => new(0); }\npublic sealed class {|#1:B|} : A;\n");
        test.TestState.Sources.Add("using Ducky; public sealed class {|#2:Store|} : SliceStore<S> { protected override S Initial => new(0); }");
        test.ExpectedDiagnostics.AddRange([At(0, "A", "S", "B', 'Store"), At(1, "B", "S", "A', 'Store"), At(2, "Store", "S", "A', 'B")]);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    public static TheoryData<string> Silent => new()
    {
        // An abstract base is not a slice of its own.
        SliceOf("Base", "S", "abstract") + "public sealed class A : Base { }",
        SliceOf("A", "S") + SliceOf("B", "T2"),
        // Different type parameters are different states.
        "public sealed class G1<TS> : Slice<List<TS>> { protected override List<TS> Initial => []; }\npublic sealed class G2<TS> : Slice<List<TS>> { protected override List<TS> Initial => []; }",
    };

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Ducky015_DistinctStates_NotReported(string source) => Verifier.VerifyAsync(Prelude + source);
}
