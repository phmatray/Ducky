using Ducky.Generators.Analyzers.Purity;
using Microsoft.CodeAnalysis;
using static Ducky.Generators.Tests.Purity.PuritySources;
using Verifier = Ducky.Generators.Tests.Infrastructure.AnalyzerVerifier<Ducky.Generators.PurityAnalyzer>;

namespace Ducky.Generators.Tests.Purity;

// DUCKY003 (SPEC §16.2), reducer and projector arms: a write to a member of a lambda parameter, to a captured local or
// parameter, or to a field, and a known collection mutator on the parameters' graph, reported with the target, what it
// mutates and the arm as arguments.
public sealed class MutationTests
{
    private const string Parameter = "a parameter";
    private const string Captured = "a captured variable";
    private const string Field = "a field";

    public static TheoryData<string, string, string, string> Reported
    {
        get
        {
            var rows = new TheoryData<string, string, string, string>();
            foreach (var arm in new[] { Reducer, Projector })
            {
                rows.Add(arm, "{ {|#0:s.Box.Value = 1|}; return s; }", "s.Box.Value", Parameter);
                rows.Add(arm, "{ {|#0:a.Box.Field += 1|}; return s; }", "a.Box.Field", Parameter);
                rows.Add(arm, "{ {|#0:s.Box.Value++|}; return s; }", "s.Box.Value", Parameter);
                rows.Add(arm, "{ {|#0:s.Box.Name ??= \"x\"|}; return s; }", "s.Box.Name", Parameter);
                rows.Add(arm, "{ {|#0:s.Array[0] = 1|}; return s; }", "s.Array[0]", Parameter);
                rows.Add(arm, "{ {|#0:s.Map[\"k\"] = 1|}; return s; }", "s.Map[\"k\"]", Parameter);
                rows.Add(arm, "{ {|#0:((Box)s.Box).Value = 1|}; return s; }", "((Box)s.Box).Value", Parameter);
                rows.Add(arm, "{ {|#0:captured = 1|}; return s; }", "captured", Captured);
                rows.Add(arm, "{ {|#0:--captured|}; return s; }", "captured", Captured);
                rows.Add(arm, "{ {|#0:seed += 1|}; return s; }", "seed", Captured);
                rows.Add(arm, "{ {|#0:log.Capacity = 1|}; return s; }", "log.Capacity", Captured);
                rows.Add(arm, "{ {|#0:_count = 1|}; return s; }", "_count", Field);
                rows.Add(arm, "{ {|#0:this._count++|}; return s; }", "this._count", Field);
                rows.Add(arm, "{ {|#0:_total += 1|}; return s; }", "_total", Field);
                rows.Add(arm, "{ {|#0:Holder.Static = 1|}; return s; }", "Holder.Static", Field);
                // Each element of a deconstruction is a write, nested tuples included; a discard is not.
                rows.Add(arm, "{ {|#0:(s.Box.Value, _) = (1, 2)|}; return s; }", "s.Box.Value", Parameter);
                rows.Add(arm, "{ {|#0:(_, (captured, _)) = (1, (2, 3))|}; return s; }", "captured", Captured);
                rows.Add(arm, "{ {|#0:(_count, _) = (1, 2)|}; return s; }", "_count", Field);
                rows.Add(arm, "{ {|#0:s.Items.Add(1)|}; return s; }", "List.Add", Parameter);
                rows.Add(arm, "{ {|#0:a.Items.Clear()|}; return s; }", "List.Clear", Parameter);
                rows.Add(arm, "{ {|#0:((ICollection<int>)s.Items).Add(1)|}; return s; }", "ICollection.Add", Parameter);
                rows.Add(arm, "{ {|#0:s.Map.Remove(\"k\")|}; return s; }", "Dictionary.Remove", Parameter);
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(Reported))]
    public Task Ducky003_Mutation_Reported(string arm, string body, string target, string owner) =>
        Verifier.VerifyAsync(Host(arm, body), Verifier.Diagnostic(PurityDiagnostics.Mutation).WithLocation(0).WithArguments(target, owner, arm));

    // Every known mutator, on a collection the projector receives directly: the parameter itself is the state graph.
    public static TheoryData<string, string, string> Mutators => new()
    {
        { "List<int>", "Add(1)", "List.Add" },
        { "List<int>", "AddRange([1])", "List.AddRange" },
        { "List<int>", "Insert(0, 1)", "List.Insert" },
        { "List<int>", "InsertRange(0, [1])", "List.InsertRange" },
        { "List<int>", "Remove(1)", "List.Remove" },
        { "List<int>", "RemoveAt(0)", "List.RemoveAt" },
        { "List<int>", "RemoveAll(x => x == 1)", "List.RemoveAll" },
        { "List<int>", "RemoveRange(0, 1)", "List.RemoveRange" },
        { "List<int>", "Clear()", "List.Clear" },
        { "List<int>", "Sort()", "List.Sort" },
        { "List<int>", "Reverse()", "List.Reverse" },
        { "HashSet<int>", "RemoveWhere(x => x == 1)", "HashSet.RemoveWhere" },
        { "HashSet<int>", "UnionWith([1])", "HashSet.UnionWith" },
        { "HashSet<int>", "IntersectWith([1])", "HashSet.IntersectWith" },
        { "HashSet<int>", "ExceptWith([1])", "HashSet.ExceptWith" },
        { "HashSet<int>", "SymmetricExceptWith([1])", "HashSet.SymmetricExceptWith" },
        { "Queue<int>", "Enqueue(1)", "Queue.Enqueue" },
        { "Queue<int>", "Dequeue()", "Queue.Dequeue" },
        { "Queue<int>", "TryDequeue(out _)", "Queue.TryDequeue" },
        { "Stack<int>", "Push(1)", "Stack.Push" },
        { "Stack<int>", "Pop()", "Stack.Pop" },
        { "Stack<int>", "TryPop(out _)", "Stack.TryPop" },
        { "Dictionary<int, int>", "TryAdd(1, 1)", "Dictionary.TryAdd" },
        { "LinkedList<int>", "AddFirst(1)", "LinkedList.AddFirst" },
        { "LinkedList<int>", "AddLast(1)", "LinkedList.AddLast" },
        { "LinkedList<int>", "RemoveFirst()", "LinkedList.RemoveFirst" },
        { "LinkedList<int>", "RemoveLast()", "LinkedList.RemoveLast" },
        { "System.Collections.ObjectModel.Collection<int>", "Add(1)", "Collection.Add" },
        { "System.Collections.ArrayList", "Add(1)", "ArrayList.Add" },
        { "System.Collections.Concurrent.ConcurrentDictionary<int, int>", "TryRemove(1, out _)", "ConcurrentDictionary.TryRemove" },
        { "System.Collections.Concurrent.ConcurrentDictionary<int, int>", "AddOrUpdate(1, 1, (_, v) => v)", "ConcurrentDictionary.AddOrUpdate" },
        { "System.Collections.Concurrent.ConcurrentDictionary<int, int>", "GetOrAdd(1, 1)", "ConcurrentDictionary.GetOrAdd" },
    };

    [Theory]
    [MemberData(nameof(Mutators))]
    public Task Ducky003_KnownMutator_Reported(string type, string call, string member) =>
        Verifier.VerifyAsync(
            Prelude + $$"""

            public sealed class Coll { public {{type}} Items { get; } = new(); }
            public static class Host
            {
                public static object Make() => Selector.Define(x => x.Get<Coll>().Items, items => { {|#0:items.{{call}}|}; return 0; });
            }
            """,
            Verifier.Diagnostic(PurityDiagnostics.Mutation).WithLocation(0).WithArguments(member, Parameter, Projector));

    // A synthesized parameter has no location: the top-level `args` is captured like any outer parameter.
    [Fact]
    public Task Ducky003_TopLevelArgs_ReportedAsCaptured()
    {
        var test = Verifier.Test(Prelude);
        test.TestState.OutputKind = OutputKind.ConsoleApplication;
        test.TestState.Sources.Add(
            ("Program.cs", "using Ducky;\n_ = Selector.Define(x => x.Get<Bag>(), b => { {|#0:args[0] = \"x\"|}; return 0; });\n"));
        test.ExpectedDiagnostics.Add(
            Verifier.Diagnostic(PurityDiagnostics.Mutation).WithLocation(0).WithArguments("args[0]", Captured, Projector));
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    public static TheoryData<string, string> Silent
    {
        get
        {
            var rows = new TheoryData<string, string>();
            foreach (var arm in new[] { Reducer, Projector })
            {
                // Rebinding the parameter itself replaces no shared value.
                rows.Add(arm, "{ s = s with { Count = 1 }; return s; }");
                // Locals and parameters declared inside the lambda, nested recipe parameters included, are not state.
                rows.Add(arm, "{ var list = new List<int>(); list.Add(1); var box = new Box(); box.Value = 2; for (var i = 0; i < 2; i++) { list.Add(i); } return s with { Count = list.Count + box.Value }; }");
                rows.Add(arm, "s.Produce(d => { d.Items.RemoveAll(x => x == 1); d.Count = 2; d.Items.Add(3); })");
                rows.Add(arm, "{ int Local(int n) { n++; return n; } return s with { Count = Local(s.Count) }; }");
                // Initializers write the object they create, `with` writes the copy.
                rows.Add(arm, "s with { Count = new Box { Value = 1, Field = 2 }.Value, Items = new List<int> { 1, 2 }, Box = new() { Name = \"x\" } }");
                // Immutable collections return a new instance; reads and non-mutating calls are pure.
                rows.Add(arm, "s with { Frozen = s.Frozen.Add(1).Remove(2).Clear(), Count = s.Items.Count + s.Map.Count + s.Items.IndexOf(1) }");
                rows.Add(arm, "s with { Items = [.. s.Items, a.Amount], Count = s.Items.ToList().Count }");
                // A known mutator on a captured collection or a field is not on the parameters' graph.
                rows.Add(arm, "{ log.Add(1); _ = 1; return s; }");
                // A discard, a tuple deconstruction and a call result are not a member of the state.
                rows.Add(arm, "{ var (x, y) = (1, 2); _ = s.Items.GetRange(0, 0).Count; return s with { Count = x + y }; }");
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(Silent))]
    public Task Ducky003_NoSharedMutation_NotReported(string arm, string body) => Verifier.VerifyAsync(Host(arm, body));
}
