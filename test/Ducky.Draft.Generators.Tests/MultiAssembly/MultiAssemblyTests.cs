using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Ducky.Draft.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Draft.Generators.Tests.MultiAssembly;

// SPEC §16.3 (INV-25, INV-26) for Ducky.Draft: three compilations with warnings as errors. Lib (MyCompany.Todos) runs
// the Draft generator; App runs it and references Lib's image, using a Lib draftable record as a member and as a list
// element, and deriving a record from a Lib draftable (`new` over the inherited Draft and CreateDraft); Tests references
// both images without the generator and runs recipes across the three, including a nested Lib draft used after Produce
// returned (cross-assembly revocation). Two Lib variants have no compatible Draft: App gets DUCKY107 plain members.
public sealed class MultiAssemblyTests
{
    private const string Undocumented = "#pragma warning disable CS1591 // justification: fixture sources are undocumented; only generated code is checked\n";

    private const string Lib = Undocumented + """
        namespace MyCompany.Todos;
        [Ducky.Draft.Draftable] public sealed partial record Todo(int Id, string Title, bool Done) { {{draft}} }
        [Ducky.Draft.Draftable] public partial record Entity(int Id);
        """;

    private const string App = Undocumented + """
        using System.Collections.Immutable;
        using MyCompany.Todos;
        namespace App;
        [Ducky.Draft.Draftable] public sealed partial record Board(Todo Selected, ImmutableList<Todo> Items);
        [Ducky.Draft.Draftable] public sealed partial record Project(int Id, string Name) : Entity(Id);
        """;

    private const string Tests = Undocumented + """
        using System.Collections.Immutable;
        using MyCompany.Todos;
        namespace Tests;
        public static class Calls
        {
            public static string Run()
            {
                var todo = new Todo(1, "a", false);
                var board = new App.Board(todo, ImmutableList.Create(todo, new Todo(2, "b", false)));
                var edited = board.Produce(d => { d.Selected.Title = "x"; d.Items[0].Done = true; });
                var reverted = board.Produce(d => { d.Selected.Title = "y"; d.Selected.Title = "a"; });
                var project = new App.Project(1, "p");
                App.Project.Draft draft = project.CreateDraft();
                draft.Name = "q";
                Entity entity = new Entity(3).CreateDraft().Build();
                Todo.Draft? kept = null;
                board.Produce(d => kept = d.Selected);
                string late;
                try { kept!.Title = "late"; late = "written"; }
                catch (System.ObjectDisposedException) { late = "revoked"; }
                return string.Join(" ", edited.Selected.Title, edited.Items[0].Done, ReferenceEquals(edited.Items[1], board.Items[1]),
                    ReferenceEquals(reverted, board), draft.Build().Name, project.Produce(d => d.Id = 7).Id, entity.Id, late);
            }
        }
        """;

    // Tests over a Lib without a compatible Draft: the members are plain get/set, edited by assignment.
    private const string PlainTests = Undocumented + """
        using System.Collections.Immutable;
        using MyCompany.Todos;
        namespace Tests;
        public static class Calls
        {
            public static string Run()
            {
                var board = new App.Board(new Todo(1, "a", false), ImmutableList<Todo>.Empty);
                var edited = board.Produce(d => { d.Selected = d.Selected with { Title = "x" }; d.Items.Add(new Todo(2, "b", true)); });
                return string.Join(" ", edited.Selected.Title, edited.Items.Count, new App.Project(1, "p").Produce(d => d.Name = "q").Name);
            }
        }
        """;

    [Fact]
    public void MultiAssembly_NoAmbiguityOrConflict()
    {
        var lib = Generate(Strict("MyCompany.Todos", Lib.Replace("{{draft}}", "", StringComparison.Ordinal)), out var libDiagnostics);
        libDiagnostics.ShouldBeEmpty();
        lib.ShouldCompileClean();

        var app = Generate(Strict("App", App).AddReferences(MetadataReference.CreateFromImage(Emit(lib))), out var appDiagnostics);
        appDiagnostics.ShouldBeEmpty();
        app.ShouldCompileClean();
        var draft = app.GetTypeByMetadataName("App.Board+Draft")!;
        draft.GetMembers("Selected").OfType<IPropertySymbol>().Single().Type.ToDisplayString().ShouldBe("MyCompany.Todos.Todo.Draft");
        draft.GetMembers("Items").OfType<IPropertySymbol>().Single().Type.ToDisplayString()
            .ShouldBe("Ducky.Draft.ListDraft<MyCompany.Todos.Todo, MyCompany.Todos.Todo.Draft>");

        // No CS0121, CS0433 or CS0436, nor any other diagnostic, and the recipes run across the three assemblies.
        Run(lib, app, Strict("Tests", Tests)).ShouldBe("x True True True q 7 3 revoked");
    }

    // A Lib that references the Ducky.Draft runtime without running its generator, and one whose hand-written nested
    // Draft implements IDraft<Todo> without a public Draft(Todo) constructor: App's Todo members become plain get/set with
    // DUCKY107, and App still compiles and runs.
    [Theory]
    [InlineData("")]
    [InlineData("public sealed class Draft : Ducky.Draft.IDraft<Todo> { bool Ducky.Draft.IDraft<Todo>.IsDirty => false; Todo Ducky.Draft.IDraft<Todo>.Build() => new(0, \"\", false); void Ducky.Draft.IRevocable.Revoke() { } }")]
    public void MultiAssembly_LibWithoutCompatibleDraft_Ducky107PlainMembers(string handWritten)
    {
        var lib = Strict("MyCompany.Todos", Lib.Replace("{{draft}}", handWritten, StringComparison.Ordinal));
        lib.ShouldCompileClean();

        var app = Generate(Strict("App", App).AddReferences(MetadataReference.CreateFromImage(Emit(lib))), out var appDiagnostics);
        app.ShouldCompileClean();
        appDiagnostics.Select(d => $"{d.Id} {d.GetMessage(CultureInfo.InvariantCulture)}").ShouldBe([
            "DUCKY107 'App.Board.Selected' gets no draft of 'MyCompany.Todos.Todo': 'MyCompany.Todos.Todo' is [Draftable] in another assembly but has no nested public Draft implementing IDraft<MyCompany.Todos.Todo> with a public Draft(MyCompany.Todos.Todo) constructor; it is edited by assignment",
            "DUCKY107 'App.Board.Items' gets no draft of 'MyCompany.Todos.Todo': 'MyCompany.Todos.Todo' is [Draftable] in another assembly but has no nested public Draft implementing IDraft<MyCompany.Todos.Todo> with a public Draft(MyCompany.Todos.Todo) constructor; it is edited by assignment",
        ]);

        Run(lib, app, Strict("Tests", PlainTests)).ShouldBe("x 1 q");
    }

    // TreatWarningsAsErrors on top of the GEN-06 consumer settings.
    private static CSharpCompilation Strict(string assemblyName, string source)
    {
        var compilation = GeneratorHarness.Compile(source).WithAssemblyName(assemblyName);
        return compilation.WithOptions(compilation.Options.WithGeneralDiagnosticOption(ReportDiagnostic.Error));
    }

    private static Compilation Generate(Compilation compilation, out ImmutableArray<Diagnostic> diagnostics)
    {
        GeneratorHarness.Run(new DraftGenerator(), compilation, out var output, out diagnostics);
        return output;
    }

    // Tests references Lib's and App's images, as a consumer references built assemblies, and runs in one collectible
    // context with them (Ducky.Draft resolves from the test host).
    private static string Run(Compilation lib, Compilation app, CSharpCompilation tests)
    {
        var images = new[] { lib, app }.Select(Emit).ToList();
        var compiled = tests.AddReferences(images.Select(i => MetadataReference.CreateFromImage(i)));
        compiled.ShouldCompileClean();
        var context = new System.Runtime.Loader.AssemblyLoadContext("multi-assembly", isCollectible: true);
        try
        {
            images.ForEach(i => context.LoadFromStream(new MemoryStream(i)));
            var calls = context.LoadFromStream(new MemoryStream(Emit(compiled))).GetType("Tests.Calls")!;
            return calls.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!.CreateDelegate<Func<string>>()();
        }
        finally
        {
            context.Unload();
        }
    }

    private static byte[] Emit(Compilation compilation)
    {
        using var image = new MemoryStream();
        compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        return image.ToArray();
    }
}
