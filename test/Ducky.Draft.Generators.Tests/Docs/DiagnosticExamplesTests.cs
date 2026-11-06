using System.Reflection;
using Ducky.Draft.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ducky.Draft.Generators.Tests.Docs;

// SPEC §16.2: every Ducky.Draft.Generators descriptor has docs/diagnostics/{ID}.md, its HelpLinkUri points there, and the
// page's bad example is a #region of this file, a raw string that the Draft generator and analyzers report with exactly
// that one Ducky ID.
public sealed class DiagnosticExamplesTests
{
    private const string HelpRoot = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/";

    #region DUCKY101
    private const string Ducky101 = """
        using Ducky.Draft;

        [Draftable]
        public sealed record Todo(string Title, bool Done);
        """;
    #endregion

    #region DUCKY102
    private const string Ducky102 = """
        using Ducky.Draft;

        public static class Todos
        {
            [Draftable]
            public sealed partial record Todo(string Title, bool Done);
        }
        """;
    #endregion

    #region DUCKY103
    private const string Ducky103 = """
        using System.Collections.Immutable;
        using Ducky.Draft;

        [Draftable]
        public sealed partial record Page<T>(ImmutableList<T> Items, int Number);
        """;
    #endregion

    #region DUCKY104
    private const string Ducky104 = """
        using Ducky.Draft;

        [Draftable]
        public sealed partial record Message(string Subject, bool Draft);
        """;
    #endregion

    #region DUCKY105
    private const string Ducky105 = """
        using System.Collections.Generic;
        using Ducky.Draft;

        [Draftable]
        public sealed partial record Cart(List<string> Items);
        """;
    #endregion

    #region DUCKY106
    private const string Ducky106 = """
        using Ducky.Draft;

        [Draftable]
        public sealed partial record Todo(string Title);

        public sealed class Editor
        {
            private Todo.Draft? _last;

            public Todo Rename(Todo todo, string title) => todo.Produce(d =>
            {
                d.Title = title;
                _last = d;
            });
        }
        """;
    #endregion

    #region DUCKY107
    // Shared.dll, built without the Ducky.Draft generator.
    private const string Ducky107Library = """
        namespace Shared;

        [Ducky.Draft.Draftable]
        public sealed partial record Address(string City);
        """;

    // The application, which references Shared.dll.
    private const string Ducky107 = """
        using Ducky.Draft;

        [Draftable]
        public sealed partial record Customer(string Name, Shared.Address Address);
        """;
    #endregion

    #region DUCKY108
    private const string Ducky108 = """
        using System;
        using Ducky.Draft;

        [Draftable, Obsolete("Use TodoV2", error: true)]
        public sealed partial record Todo(string Title);
        """;
    #endregion

    #region DUCKY109
    private const string Ducky109 = """
        using System.Collections.Immutable;
        using Ducky.Draft;

        public sealed record Job(int Id);

        [Draftable]
        public sealed partial record Jobs(ImmutableQueue<Job> Pending);

        public static class Scheduler
        {
            public static Jobs Schedule(Jobs jobs, Job job) => jobs.Produce(d => { d.Pending.Enqueue(job); });
        }
        """;
    #endregion

    public static TheoryData<string, string, string?> Examples => new()
    {
        { "DUCKY101", Ducky101, null },
        { "DUCKY102", Ducky102, null },
        { "DUCKY103", Ducky103, null },
        { "DUCKY104", Ducky104, null },
        { "DUCKY105", Ducky105, null },
        { "DUCKY106", Ducky106, null },
        { "DUCKY107", Ducky107, Ducky107Library },
        { "DUCKY108", Ducky108, null },
        { "DUCKY109", Ducky109, null },
    };

    [Theory]
    [MemberData(nameof(Examples))]
    public async Task DocsExample_ReportsExactlyItsId(string id, string source, string? library)
    {
        var compilation = GeneratorHarness.Compile(source);
        if (library is not null)
        {
            compilation = compilation.AddReferences(GeneratorHarness.Compile(library).WithAssemblyName("Shared").ToMetadataReference());
        }

        CSharpGeneratorDriver.Create([new DraftGenerator().AsSourceGenerator()], parseOptions: GeneratorHarness.ParseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generated, TestContext.Current.CancellationToken);
        var analyzed = await output.WithAnalyzers([new DraftEscapeAnalyzer(), new DraftDiscardAnalyzer()])
            .GetAllDiagnosticsAsync(TestContext.Current.CancellationToken);

        // The example is otherwise valid code: no compiler error, only the one Ducky ID.
        analyzed.Where(d => d.Severity == DiagnosticSeverity.Error && !d.Id.StartsWith("DUCKY", StringComparison.Ordinal))
            .Select(d => d.ToString()).ShouldBeEmpty();
        generated.Concat(analyzed).Select(d => d.Id).Where(d => d.StartsWith("DUCKY", StringComparison.Ordinal)).Distinct().ShouldBe([id]);
    }

    // Every descriptor the assembly declares, found by reflection so a new one can't miss its page, link or example.
    private static IEnumerable<DiagnosticDescriptor> AllDescriptors() => typeof(DraftGenerator).Assembly.GetTypes()
        .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
        .Select(f => (DiagnosticDescriptor)f.GetValue(null)!);

    public static TheoryData<string> Descriptors => [.. AllDescriptors().Select(d => d.Id).Distinct()];

    [Theory]
    [MemberData(nameof(Descriptors))]
    public void EveryDiagnostic_HasDocsPageAndHelpLink(string id)
    {
        AllDescriptors().Where(d => d.Id == id).ShouldAllBe(d => d.HelpLinkUri == $"{HelpRoot}{id}.md");
        var page = Path.Combine(RepositoryRoot(), "docs", "diagnostics", $"{id}.md");
        File.Exists(page).ShouldBeTrue($"docs/diagnostics/{id}.md is missing");
        File.ReadAllText(page).ShouldContain($"[!code-csharp[](../../test/Ducky.Draft.Generators.Tests/Docs/DiagnosticExamplesTests.cs#{id})]", Case.Sensitive);
        Examples.Select(row => row.Data.Item1).ShouldContain(id);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "docs", "spec", "SPEC.md")))
        {
            directory = directory.Parent.ShouldNotBeNull("docs/spec/SPEC.md not found above the test output");
        }

        return directory.FullName;
    }
}
