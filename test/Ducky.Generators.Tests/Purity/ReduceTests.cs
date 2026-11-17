using Ducky.Generators.Analyzers.Purity;
using static Ducky.Generators.Tests.Purity.PuritySources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.PurityAnalyzer>;

namespace Ducky.Generators.Tests.Purity;

// #324: a Reduce override of Slice<TState, TAction> is a reducer like an On<T> lambda: DUCKY001-003 with the same rules,
// its own parameters standing for the lambda's. A Reduce that is not such an override is not analysed.
public sealed class ReduceTests
{
    private const string Parameter = "a parameter";
    private const string Field = "a field";

    private static string Slice(string body) =>
        Prelude + $$"""

            public sealed class Host : Slice<Bag, Hit>
            {
                private int _count;
                protected override Bag Initial => new(0, [], [], [], [], new());
                protected override Bag Reduce(Bag s, Hit a) {{body}}
            }
            """;

    [Fact]
    public Task Ducky001_NonDeterministicMember_Reported() =>
        Verifier.VerifyAsync(Slice("=> s with { Count = {|#0:DateTime.Now|}.Second };"),
            Verifier.Diagnostic(PurityDiagnostics.NonDeterministic).WithLocation(0).WithArguments("DateTime.Now", Reducer));

    [Fact]
    public Task Ducky002_Io_Reported() =>
        Verifier.VerifyAsync(Slice("{ {|#0:Console.WriteLine()|}; return s; }"),
            Verifier.Diagnostic(PurityDiagnostics.Io).WithLocation(0).WithArguments("Console.WriteLine", Reducer));

    [Theory]
    [InlineData("{ {|#0:s.Box.Value = 1|}; return s; }", "s.Box.Value", Parameter)]
    [InlineData("{ {|#0:a.Items.Add(1)|}; return s; }", "List.Add", Parameter)]
    [InlineData("{ {|#0:_count++|}; return s; }", "_count", Field)]
    public Task Ducky003_Mutation_Reported(string body, string target, string owner) =>
        Verifier.VerifyAsync(Slice(body),
            Verifier.Diagnostic(PurityDiagnostics.Mutation).WithLocation(0).WithArguments(target, owner, Reducer));

    // Rebinding a parameter and writing a local replace no shared value, as in a lambda.
    [Fact]
    public Task PureReduce_NotReported() =>
        Verifier.VerifyAsync(Slice("{ var count = s.Count; count++; s = s with { Count = count + a.Amount }; return s; }"));

    // An override of an override is still a reducer; a Reduce on a type that isn't a union slice is not.
    [Fact]
    public Task DerivedOverride_Reported() =>
        Verifier.VerifyAsync(Prelude + """

            public abstract class Base : Slice<Bag, Hit>
            {
                protected override Bag Initial => new(0, [], [], [], [], new());
                protected override Bag Reduce(Bag s, Hit a) => s;
            }

            public sealed class Host : Base
            {
                protected override Bag Reduce(Bag s, Hit a) => s with { Count = {|#0:DateTime.Now|}.Second };
            }
            """,
            Verifier.Diagnostic(PurityDiagnostics.NonDeterministic).WithLocation(0).WithArguments("DateTime.Now", Reducer));

    // Only Reduce: a RegisterCases override of the same base is not a reducer.
    [Fact]
    public Task RegisterCases_NotReported() =>
        Verifier.VerifyAsync(Slice("=> s;").Replace("    private int _count;", "    private int _count;\n    protected override void RegisterCases() => _count = DateTime.Now.Second;", StringComparison.Ordinal));

    [Fact]
    public Task OtherReduce_NotReported() =>
        Verifier.VerifyAsync(Prelude + """

            public abstract class Reducer<TState, TAction>
            {
                protected abstract TState Reduce(TState s, TAction a);
            }

            public sealed class Host : Reducer<Bag, Hit>
            {
                protected override Bag Reduce(Bag s, Hit a) => s with { Count = DateTime.Now.Second };
            }
            """);
}
