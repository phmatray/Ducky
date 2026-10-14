using Ducky.Generators.Analyzers.Purity;
using Microsoft.CodeAnalysis;
using static Ducky.Generators.Tests.Purity.PuritySources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.PurityAnalyzer>;

namespace Ducky.Generators.Tests.Purity;

// DUCKY003's Draft-receiver exemption (SPEC §16.2, §24 step 17), against the real Ducky.Draft.dll (GEN-06): a write
// whose receiver implements Ducky.Draft.IDraft<T> or is a Ducky.Draft collection draft edits a draft, never the shared
// state, so it is not reported. A write to anything else reached through a draft, or to a look-alike type, still is.
public sealed class DraftExemptionTests
{
    // The real Ducky.Draft.dll, copied next to the tests as a file only: Ducky.Generators matches it by metadata name.
    private static readonly MetadataReference _draft = MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Ducky.Draft.dll"));

    private const string Field = "a field";
    private const string Captured = "a captured variable";

    public static TheoryData<string> Exempt =>
    [
        "{ _draft.Count = 1; _draft.Box = new(); return s; }",
        "{ captured.Count++; captured.Count += 2; return s; }",
        "{ ((BagDraft)_draft).Count = 1; return s; }",
        "{ _list[0] = 1; return s; }",
        "{ _map[\"k\"] = 1; return s; }",
    ];

    [Theory]
    [MemberData(nameof(Exempt))]
    public Task Ducky003_DraftReceiver_NotReported(string body) => Verify(body);

    public static TheoryData<string, string, string> Reported => new()
    {
        // Rebinding the field that holds a draft is a write to the host, not to the draft.
        { "{ {|#0:_draft = new()|}; return s; }", "_draft", Field },
        // A plain object or an array reached through a draft is shared with the state the draft was made from.
        { "{ {|#0:_draft.Box.Value = 1|}; return s; }", "_draft.Box.Value", Field },
        { "{ {|#0:captured.Array[0] = 1|}; return s; }", "captured.Array[0]", Captured },
        // IRevocable outside Ducky.Draft is no collection draft, and a Ducky.Draft type that is not revocable is none either.
        { "{ {|#0:_revocable.Count = 1|}; return s; }", "_revocable.Count", Field },
        { "{ {|#0:_lookalike.Count = 1|}; return s; }", "_lookalike.Count", Field },
    };

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky003_NotADraftReceiver_Reported(string body, string target, string owner) =>
        Verify(body, Verifier.Diagnostic(PurityDiagnostics.Mutation).WithLocation(0).WithArguments(target, owner, Reducer));

    private static Task Verify(string body, params Microsoft.CodeAnalysis.Testing.DiagnosticResult[] expected)
    {
        var test = Verifier.Test(Prelude + $$"""

            public sealed class BagDraft : Ducky.Draft.IDraft<Bag>
            {
                public int Count { get; set; }
                public Box Box { get; set; } = new();
                public int[] Array { get; } = [0];
                public bool IsDirty => false;
                public Bag Build() => new(Count, [], [], [], Array, Box);
                public void Revoke() { }
            }
            public sealed class Revocable : Ducky.Draft.IRevocable { public int Count { get; set; } public void Revoke() { } }
            namespace Ducky.Draft { public sealed class Lookalike { public int Count { get; set; } } }
            public sealed class DraftHost : Slice<Bag>
            {
                private BagDraft _draft = new();
                private readonly Ducky.Draft.ListDraft<int> _list = ImmutableList.Create(0);
                private readonly Ducky.Draft.DictionaryDraft<string, int> _map = ImmutableDictionary<string, int>.Empty;
                private readonly Revocable _revocable = new();
                private readonly Ducky.Draft.Lookalike _lookalike = new();
                protected override Bag Initial => new(0, [], [], [], [], new());

                public DraftHost(BagDraft captured)
                {
                    On<Hit>((s, a) => {{body}});
                }
            }
            """);
        test.TestState.AdditionalReferences.Add(_draft);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }
}
