using Ducky.Generators.Analyzers.Structural;
using static Ducky.Generators.Tests.Structural.StructuralSources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.StructuralAnalyzer>;

namespace Ducky.Generators.Tests.Structural;

// DUCKY011 (SPEC §16.2, ADR-0007): `s with { }` on the state parameter of an On<T> reducer lambda is a new reference,
// so the slice notifies on every action. Reported on the `with` expression, whose text is the argument.
public sealed class EmptyWithTests
{
    public static TheoryData<string, string> Reported => new()
    {
        { Slice("On<Hit>((s, a) => {|#0:s with { }|});"), "s with { }" },
        { Slice("On<Hit>(s => {|#0:s with {}|});"), "s with {}" },
        { Slice("On<Hit>((state, a) => { return {|#0:state with { }|}; });"), "state with { }" },
        { Slice("On<Hit>((s, a) => a.Id > 0 ? s with { Count = a.Id } : {|#0:s with { }|});"), "s with { }" },
        { Slice("On<Hit>((s, a) => new List<Hit> { a }.Count > 0 ? s : Run(() => {|#0:s with { }|})); static S Run(Func<S> f) => f();"), "s with { }" },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky011_EmptyWithOnState_Reported(string source, string text) =>
        Verifier.VerifyAsync(source, Verifier.Diagnostic(StructuralDiagnostics.EmptyWith).WithLocation(0).WithArguments(text));

    public static TheoryData<string> Silent => new()
    {
        // A change, the state itself, and a copy of something other than the state.
        Slice("On<Hit>((s, a) => s with { Count = a.Id }); On<Miss>((s, a) => a with { } is null ? s : s); On<T2>(s => new S(1) with { });"),
        // A stored delegate is not analysed.
        Slice("Func<S, Hit, S> reduce = (s, a) => s with { }; On<Hit>(reduce);"),
        // A method group is not analysed, and `with` outside a reducer is not a reducer result.
        Slice("On<Hit>(Reduce); static S Reduce(S s, Hit a) => s with { }; var copy = new S(1) with { };"),
        Prelude + "public sealed class Other { public S Copy(S s) => s with { }; }",
        // An effect group handler is not a reducer.
        Group("On<Hit>((a, ctx, ct) => Task.FromResult(a with { }));"),
    };

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Ducky011_NoEmptyWithOnState_NotReported(string source) => Verifier.VerifyAsync(source);
}
