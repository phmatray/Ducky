using Ducky.Generators.Analyzers.Purity;
using Microsoft.CodeAnalysis.Testing;
using static Ducky.Generators.Tests.Purity.PuritySources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.PurityAnalyzer>;

namespace Ducky.Generators.Tests.Purity;

// DUCKY001–003 (SPEC §12, §16.2), the SliceStore mutator arm (§24 step 8): a lambda passed to SliceStore.Set or SetAsync
// is analysed like an On<T> reducer, against the real Ducky.dll. The mutator is the first parameter, so it is found by
// parameter even when a named argument moves it; the `name` argument is not a mutator.
public sealed class MutatorArmTests
{
    private const string Mutator = "mutator";

    private static string Store(string call, string members = "") => Prelude + $$"""

        public sealed class BagStore : SliceStore<Bag>
        {
            private int _count;
            protected override Bag Initial => new(0, [], [], [], [], new());
            {{members}}

            public void Run(Task<int> task, int seed)
            {
                var captured = 0;
                {{call}};
            }
        }
        """;

    public static TheoryData<string, string, string, string> Reported
    {
        get
        {
            var rows = new TheoryData<string, string, string, string>();
            foreach (var method in new[] { "Set", "_ = SetAsync" })
            {
                rows.Add(method + "(s => s with { Count = {|#0:DateTime.Now|}.Second })", "DUCKY001", "DateTime.Now", "");
                rows.Add(method + "(s => s with { Count = {|#0:Guid.NewGuid()|}.GetHashCode() })", "DUCKY001", "Guid.NewGuid", "");
                rows.Add(method + "(s => { {|#0:Console.WriteLine()|}; return s; })", "DUCKY002", "Console.WriteLine", "");
                rows.Add(method + "(s => s with { Count = {|#0:task.Result|} })", "DUCKY002", "Task.Result", "");
                rows.Add(method + "(s => { {|#0:s.Box.Value = 1|}; return s; })", "DUCKY003", "s.Box.Value", "a parameter");
                rows.Add(method + "(s => { {|#0:s.Items.Add(1)|}; return s; })", "DUCKY003", "List.Add", "a parameter");
                rows.Add(method + "(s => { {|#0:captured = seed|}; return s; })", "DUCKY003", "captured", "a captured variable");
                rows.Add(method + "(s => { {|#0:_count++|}; return s; })", "DUCKY003", "_count", "a field");
                rows.Add(method + "(name: \"Bump\", mutator: s => { {|#0:s.Box.Value = 1|}; return s; })", "DUCKY003", "s.Box.Value", "a parameter");
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Purity_SliceStoreMutator_Reported(string call, string id, string target, string owner)
    {
        var descriptor = id switch
        {
            "DUCKY001" => PurityDiagnostics.NonDeterministic,
            "DUCKY002" => PurityDiagnostics.Io,
            _ => PurityDiagnostics.Mutation,
        };
        var expected = Verifier.Diagnostic(descriptor).WithLocation(0);
        return Verifier.VerifyAsync(
            Store(call),
            owner.Length == 0 ? expected.WithArguments(target, Mutator) : expected.WithArguments(target, owner, Mutator));
    }

    // Each Set call is its own arm: two mutators, two reports.
    [Fact]
    public Task Purity_SliceStoreMutatorAndReducer_EachReported() =>
        Verifier.VerifyAsync(
            Store("Set(s => s with { Count = {|#0:DateTime.UtcNow|}.Second })", "public BagStore() => On<Hit>((s, a) => s with { Count = {|#1:Environment.TickCount|} });"),
            Verifier.Diagnostic(PurityDiagnostics.NonDeterministic).WithLocation(0).WithArguments("DateTime.UtcNow", Mutator),
            Verifier.Diagnostic(PurityDiagnostics.NonDeterministic).WithLocation(1).WithArguments("Environment.TickCount", Reducer));

    public static TheoryData<string> Silent =>
    [
        // Pure mutators, the §12 CartStore shapes.
        "Set(s => s with { Count = s.Count + 1, Frozen = s.Frozen.Add(seed) })",
        "_ = SetAsync(_ => Initial)",
        "Set(s => s.Produce(d => { d.Items.Add(1); d.Count = 2; }))",
        // The name is not the mutator, and a method group or a stored delegate is not analysed.
        "Set(s => s, DateTime.Now.ToString())",
        "Set(Bump)",
        "Func<Bag, Bag> stored = s => { s.Box.Value = 1; return s; }; Set(stored)",
        // A Set that isn't SliceStore's, even on a SliceStore subclass.
        "Other.Set(s => { s.Box.Value = 1; return s; })",
        "new Hiding().Set(s => { s.Box.Value = 1; return s; })",
    ];

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Purity_SliceStoreNonMutator_NotReported(string call)
    {
        var source = Store(call, "private static Bag Bump(Bag s) { s.Box.Value = 1; return s; }") + """

            public static class Other { public static void Set(Func<Bag, Bag> mutator) { } }
            public sealed class Hiding : SliceStore<Bag>
            {
                protected override Bag Initial => new(0, [], [], [], [], new());
                public new void Set(Func<Bag, Bag> mutator, string name = "") { }
            }
            """;
        return Verifier.VerifyAsync(source, DiagnosticResult.EmptyDiagnosticResults);
    }
}
