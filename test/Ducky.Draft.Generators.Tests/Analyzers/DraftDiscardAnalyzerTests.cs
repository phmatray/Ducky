using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Verifier = Ducky.Draft.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Draft.Generators.DraftDiscardAnalyzer>;

namespace Ducky.Draft.Generators.Tests.Analyzers;

// DUCKY109 verifier tests (SPEC §16.2): inside a Produce recipe, an expression statement discards the result of a call
// whose receiver is a plain get/set draft member and whose return type is the receiver's type, so the edit is lost and
// Produce returns the base. The verifier runs the Draft generator first, as in a consumer build, over the real
// Ducky.Draft.dll and the real Ducky.dll (EntityState). `{|DUCKY109:...|}` marks each expected location.
public sealed class DraftDiscardAnalyzerTests
{
    // The real Ducky.dll, copied next to the tests as a file only (GEN-06): EntityState<TKey, TEntity>.Upsert.
    private static readonly MetadataReference _ducky = MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Ducky.dll"));

    private const string Prologue = """
        #pragma warning disable CS1591 // justification: undocumented test sources
        using System;
        using System.Collections.Immutable;
        using Ducky;
        namespace App;
        public sealed record Job(int Id);
        public sealed record Item(int Id) : IEntity<int>;
        [Ducky.Draft.Draftable] public sealed partial record Child(ImmutableQueue<Job> Pending);
        [Ducky.Draft.Draftable] public sealed partial record Jobs(
            ImmutableQueue<Job> Pending, ImmutableStack<Job> Undo, ImmutableSortedDictionary<int, Job> Index, EntityState<int, Item> Todos,
            string Title, Child Child, ImmutableList<Child> Children, ImmutableList<ImmutableQueue<Job>> Queues)
        { public string Upper => Title.ToUpperInvariant(); }
        public sealed record Rec(int Value);
        public sealed class Hand : Ducky.Draft.IDraft<Rec>
        {
            public Hand? Next { get; set; }
            public ImmutableQueue<Job> Pending { get; set; } = [];
            public bool IsDirty => false;
            public Hand Self() => this;
            public Rec Build() => new(0);
            public void Revoke() { }
            public static Rec Produce(Action<Hand> recipe) { recipe(new Hand()); return new(0); }
        }
        public sealed class Box { public ImmutableQueue<Job> Pending { get; set; } = []; public void Produce(Action<Box> recipe) => recipe(this); }
        public sealed class Pair { public Jobs Produce(Jobs j, Action<Jobs.Draft, int> recipe) { recipe(j.CreateDraft(), 0); return j; } }
        public static class Other { public static void Apply(this Jobs j, Action<Jobs.Draft> recipe) => recipe(j.CreateDraft()); }
        public static class Statics { public static ImmutableQueue<Job> Queue { get; set; } = []; }
        public sealed class Recipes
        {
            private static readonly Job J = new(1);

        """;

    public static TheoryData<string, string> Discarded => new()
    {
        { "ImmutableQueue.Enqueue", "public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Pending.Enqueue(J)|}; });" },
        { "ImmutableSortedDictionary.Add", "public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Index.Add(1, J)|}; });" },
        { "EntityState.Upsert", "public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Todos.Upsert(new Item(1))|}; });" },
        { "ImmutableStack.Push", "public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Undo.Push(J)|}; });" },
        { "string.Trim", "public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Title.Trim()|}; });" },
        { "an expression-bodied recipe", "public Jobs M(Jobs t) => t.Produce(d => {|DUCKY109:d.Pending.Enqueue(J)|});" },
        { "a member of a nested draft", "public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Child.Pending.Enqueue(J)|}; });" },
        { "a member of an element draft", "public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Children[0].Pending.Enqueue(J)|}; });" },
        { "an element of a plain list draft", "public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Queues[0].Enqueue(J)|}; });" },
        { "a member of another draft", "public Jobs M(Jobs t) => t.Produce(d => { var o = t.CreateDraft(); {|DUCKY109:o.Pending.Enqueue(J)|}; });" },
        { "a nested lambda", "public Jobs M(Jobs t) => t.Produce(d => { Action a = () => {|DUCKY109:d.Pending.Enqueue(J)|}; a(); });" },
        { "a local function", "public Jobs M(Jobs t) => t.Produce(d => { void Add() { {|DUCKY109:d.Pending.Enqueue(J)|}; } Add(); });" },
        { "a hand-written draft", "public Rec M() => Hand.Produce(h => { {|DUCKY109:h.Pending.Enqueue(J)|}; });" },
        { "a recipe with a second, plain parameter", "public Jobs M(Jobs t, Pair p) => p.Produce(t, (d, n) => { {|DUCKY109:d.Pending.Enqueue(J)|}; });" },
        { "two discards", "public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Pending.Enqueue(J)|}; {|DUCKY109:d.Undo.Push(J)|}; });" },
    };

    public static TheoryData<string, string> Kept => new()
    {
        { "assigned", "public Jobs M(Jobs t) => t.Produce(d => { d.Pending = d.Pending.Enqueue(J); d.Index = d.Index.Add(1, J); d.Todos = d.Todos.Upsert(new Item(1)); });" },
        { "an explicit discard", "public Jobs M(Jobs t) => t.Produce(d => { _ = d.Pending.Enqueue(J); });" },
        { "another return type", "public Jobs M(Jobs t) => t.Produce(d => { d.Index.ContainsKey(1); d.Todos.Find(1); });" },
        { "a void call", "public Jobs M(Jobs t) => t.Produce(d => { d.Title.CopyTo(0, new char[1], 0, 0); });" },
        { "a local copied from a member", "public Jobs M(Jobs t) => t.Produce(d => { var q = d.Pending; q.Enqueue(J); });" },
        { "a static property", "public Jobs M(Jobs t) => t.Produce(d => { Statics.Queue.Enqueue(J); });" },
        { "a draft member returning its own draft", "public Rec M() => Hand.Produce(h => { h.Next!.Self(); });" },
        { "a member of a plain object", "public Jobs M(Jobs t, Box b) => t.Produce(d => { b.Pending.Enqueue(J); });" },
        { "a Produce whose parameter is no draft", "public void M(Jobs t, Box b) { var o = t.CreateDraft(); b.Produce(x => { o.Pending.Enqueue(J); }); }" },
        { "a get-only draft member", "public Jobs M(Jobs t) => t.Produce(d => { d.Upper.Trim(); });" },
        { "a recipe passed to another method", "public void M(Jobs t) => t.Apply(d => { d.Pending.Enqueue(J); });" },
        { "a recipe stored before Produce", "public Jobs M(Jobs t) { Action<Jobs.Draft> r = d => d.Pending.Enqueue(J); return t.Produce(r); }" },
        { "a multi-step draft", "public Jobs M(Jobs t) { var d = t.CreateDraft(); d.Pending.Enqueue(J); return d.Build(); }" },
    };

    [Theory]
    [MemberData(nameof(Discarded))]
    public Task Ducky109_ResultDiscarded_Reported(string shape, string member) => Verify(Source(member + " // " + shape));

    [Theory]
    [MemberData(nameof(Kept))]
    public Task Ducky109_ResultKept_NotReported(string shape, string member) => Verify(Source(member + " // " + shape));

    [Fact]
    public Task Ducky109_Message_NamesTheAssignment() =>
        Verify(
            Source("public Jobs M(Jobs t) => t.Produce(d => { {|#0:d.Index.Add(1, J)|}; });"),
            Verifier.Diagnostic(DraftDiagnostics.Discard).WithLocation(0).WithArguments("d.Index", "Add").WithMessage(
                "result discarded: assign it, d.Index = d.Index.Add(...)"));

    // A component's @code block compiles to generated C# (*.razor.g.cs), where most Blazor recipes live.
    [Fact]
    public Task Ducky109_GeneratedCode_Reported() =>
        Verify("// <auto-generated/>\n#nullable enable\n" + Source("public Jobs M(Jobs t) => t.Produce(d => { {|DUCKY109:d.Pending.Enqueue(J)|}; });"));

    private static Task Verify(string source, params DiagnosticResult[] expected)
    {
        var test = Verifier.Create(source);
        test.TestState.AdditionalReferences.Add(_ducky);
        return Verifier.VerifyAsync(test, expected);
    }

    private static string Source(string member) => Prologue + "    " + member + "\n}\n";
}
