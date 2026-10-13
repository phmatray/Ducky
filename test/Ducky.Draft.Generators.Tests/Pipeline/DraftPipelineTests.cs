using Ducky.Draft.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Draft.Generators.Tests.Pipeline;

// GEN-06 for the Draft generator (SPEC §13.3, §16.1): hint names never crash the generator (CS8785) and every tracked
// step caches.
public sealed class DraftPipelineTests
{
    // Every member kind of the model: scalars, a nested draftable member (nullable), a collection of draftable elements and
    // a mutable collection reported with DUCKY105.
    private const string Consumer = """
        #pragma warning disable CS1591 // justification: undocumented test source
        using System.Collections.Generic;
        using System.Collections.Immutable;
        namespace App;
        [Ducky.Draft.Draftable] public sealed partial record Todo(int Id, string Title);
        [Ducky.Draft.Draftable] public sealed partial record Board(List<int> Numbers, ImmutableList<Todo> Items, Todo? Selected);
        """;

    // A diagnosed record (DUCKY101) in its own file: its model, location included, caches like a valid one.
    private const string Invalid = """
        namespace App;
        [Ducky.Draft.Draftable] public sealed record Bad(int Id);
        """;

    // Same-named records in two namespaces, case-only names, names equal once sanitized (non-ASCII), nesting two levels
    // deep and a keyword name: each gets its own valid hint name. AddSource compares hint names ignoring case.
    [Fact]
    public void HintNames_UniqueAndValid()
    {
        var compilation = GeneratorHarness.Compile("""
            #pragma warning disable CS1591, CS8981 // justification: undocumented test source; lower-case names are the shapes under test
            namespace A { [Ducky.Draft.Draftable] public sealed partial record Todo; }
            namespace B { [Ducky.Draft.Draftable] public sealed partial record Todo(int Id); }
            namespace C
            {
                [Ducky.Draft.Draftable] public sealed partial record Inc;
                [Ducky.Draft.Draftable] public sealed partial record inc;
                [Ducky.Draft.Draftable] public sealed partial record INC;
                [Ducky.Draft.Draftable] public sealed partial record inc_2;
                [Ducky.Draft.Draftable] public sealed partial record Café;
                [Ducky.Draft.Draftable] public sealed partial record Caf_;
                public static partial class Outer { public static partial class Inner { [Ducky.Draft.Draftable] public sealed partial record Leaf; } }
                [Ducky.Draft.Draftable] public sealed partial record @class;
            }
            """);

        var driver = GeneratorHarness.Run(new DraftGenerator(), compilation, out var output);

        output.ShouldCompileClean();
        driver.GetRunResult().Results.Single().GeneratedSources.Select(s => s.HintName).ShouldBe([
            "A.Todo.Draft.g.cs",
            "B.Todo.Draft.g.cs",
            "C.Inc.Draft.g.cs",
            "C.inc_2.Draft.g.cs",
            "C.INC_3.Draft.g.cs",
            "C.inc_2_2.Draft.g.cs",
            "C.Caf_.Draft.g.cs",
            "C.Caf__2.Draft.g.cs",
            "C.Outer.Inner.Leaf.Draft.g.cs",
            "C._class.Draft.g.cs",
        ]);
    }

    [Fact]
    public void Caching_DraftRecords_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new DraftGenerator(), GeneratorHarness.Compile(Consumer, Invalid), "DraftRecords");

    [Fact]
    public void Caching_DraftOutput_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new DraftGenerator(), GeneratorHarness.Compile(Consumer, Invalid), "DraftOutput");

    [Fact]
    public void Caching_DraftAnalyses_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new DraftGenerator(), GeneratorHarness.Compile(Consumer, Invalid), "DraftAnalyses");

    [Fact]
    public void Caching_DraftReports_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new DraftGenerator(), GeneratorHarness.Compile(Consumer, Invalid), "DraftReports");

    // A valid record's model carries no location, so an edit that only moves it within its file emits nothing again: only
    // its member reports run again, at the moved location.
    [Fact]
    public void Caching_DraftOutput_RecordMovedInFile_Cached()
    {
        var compilation = GeneratorHarness.Compile(Consumer, Invalid);
        var tree = compilation.SyntaxTrees.First();
        var driver = GeneratorHarness.Run(new DraftGenerator(), compilation, out _, out var before);
        var edited = compilation.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText("// moved\n" + Consumer, GeneratorHarness.ParseOptions, tree.FilePath, cancellationToken: TestContext.Current.CancellationToken));

        var result = driver.RunGenerators(edited, TestContext.Current.CancellationToken).GetRunResult().Results.Single();

        var emitting = result.TrackedOutputSteps.Values.SelectMany(s => s).Where(s => s.Inputs.Any(i => i.Source.Name == "DraftOutput")).ToList();
        emitting.ShouldNotBeEmpty();
        result.TrackedSteps["DraftRecords"].Concat(result.TrackedSteps["DraftOutput"]).Concat(emitting)
            .SelectMany(s => s.Outputs)
            .ShouldAllBe(o => o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged);
        Line(result.Diagnostics).ShouldBe(Line(before) + 1);
    }

    private static int Line(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics.Single(d => d.Id == "DUCKY105").Location.GetLineSpan().StartLinePosition.Line;
}
