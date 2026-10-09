using System.ComponentModel;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Blazor.Tests.Migration;

// SPEC §22.1, ADR-0038: the 1.x tombstones of Ducky and Ducky.Blazor. Non-normative compilation tests: test code can't
// name an error-level [Obsolete] type, so each 1.x usage is compiled from source. The regions are the "bad example"
// snippets of docs/diagnostics/DUCKYM00n.md.
public sealed class TombstoneTests
{
    private const string UrlFormat = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{0}.md";

    #region DUCKYM001
    private const string M001 = """
        using Ducky;

        public sealed record CounterState(int Value) : IState;

        public sealed record AddTodo(string Key) : IKeyedAction;
        """;
    #endregion

    #region DUCKYM002
    private const string M002 = """
        using Ducky;

        public sealed record CounterState(int Value);

        public sealed class CounterReducers : SliceReducers<CounterState>
        {
            public override CounterState GetInitialState() => new(0);
        }

        public sealed class CounterSlice : ISlice<CounterState>;
        """;
    #endregion

    #region DUCKYM003
    private const string M003 = """
        using Ducky;

        public sealed record LoadTodos;

        public sealed class LoadTodosEffect : AsyncEffect<LoadTodos>;

        public sealed class LogEffect : IAsyncEffect;
        """;
    #endregion

    #region DUCKYM004
    private const string M004 = """
        using Ducky;

        public sealed class SearchEffect : IReactiveEffect;
        """;
    #endregion

    #region DUCKYM005
    private const string M005 = """
        using Ducky;

        public sealed record CounterState(int Value);

        public static class CounterSelectors
        {
            public static readonly object Doubled = MemoizedSelector.Create((CounterState state) => state.Value * 2);
        }
        """;
    #endregion

    #region DUCKYM006
    private const string M006 = """
        using Ducky;

        public sealed record Reset : Fsa;

        public sealed record Increment(int Payload) : Fsa<int>;

        public sealed record Rename(string Payload, string Meta) : Fsa<string, string>;

        public sealed record LoadFailed : FsaError;

        public sealed record SaveFailed(string Meta) : FsaError<string>;

        public sealed record Tagged(string Meta) : FsaMeta<string>;
        """;
    #endregion

    #region DUCKYM007
    private const string M007 = """
        using Ducky;

        public sealed record Todo(string Id, string Title);

        public sealed record TodoState : NormalizedState<string, Todo, TodoState>;
        """;
    #endregion

    #region DUCKYM008
    private const string M008 = """
        using Ducky.Blazor;

        public sealed record LayoutState(bool Collapsed);

        public abstract class MainLayout : DuckyLayout<LayoutState>;
        """;
    #endregion

    // One DUCKYM error per tombstone usage, in source order; M002's override adds the CS0115 any member-less tombstone
    // gives (§22.1), M005's static call the CS0117.
    public static TheoryData<string, string, string[]> Usages => new()
    {
        { "DUCKYM001", M001, ["DUCKYM001", "DUCKYM001"] },
        { "DUCKYM002", M002, ["DUCKYM002", "CS0115", "DUCKYM002"] },
        { "DUCKYM003", M003, ["DUCKYM003", "DUCKYM003"] },
        { "DUCKYM004", M004, ["DUCKYM004"] },
        { "DUCKYM005", M005, ["DUCKYM005", "CS0117"] },
        { "DUCKYM006", M006, ["DUCKYM006", "DUCKYM006", "DUCKYM006", "DUCKYM006", "DUCKYM006", "DUCKYM006"] },
        { "DUCKYM007", M007, ["DUCKYM007"] },
        { "DUCKYM008", M008, ["DUCKYM008"] },
    };

    [Theory]
    [MemberData(nameof(Usages))]
    public void Tombstones_DeriveFromEach_ReportsDuckyM00n(string id, string source, string[] expected)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            id,
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToList();

        errors.Select(d => d.Id).ShouldBe(expected, string.Join(Environment.NewLine, errors));
        errors.Where(d => d.Id == id).ShouldAllBe(d => d.Descriptor.HelpLinkUri == UrlFormat.Replace("{0}", id, StringComparison.Ordinal));
    }

    // §22.1: member-less hidden interfaces for exactly the published 1.0.292 base types (no dev-only type, no IMiddleware).
    [Fact]
    public void Tombstones_AreMemberlessHiddenInterfaces()
    {
        var tombstones = new[] { typeof(IStore).Assembly, typeof(DuckyLayout).Assembly }
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.GetCustomAttribute<ObsoleteAttribute>() is not null)
            .ToList();

        tombstones.Select(type => (type.FullName, type.GetCustomAttribute<ObsoleteAttribute>()!.DiagnosticId)).ShouldBe(
            [
                ("Ducky.IState", "DUCKYM001"),
                ("Ducky.IKeyedAction", "DUCKYM001"),
                ("Ducky.SliceReducers`1", "DUCKYM002"),
                ("Ducky.ISlice`1", "DUCKYM002"),
                ("Ducky.AsyncEffect`1", "DUCKYM003"),
                ("Ducky.IAsyncEffect", "DUCKYM003"),
                ("Ducky.IReactiveEffect", "DUCKYM004"),
                ("Ducky.MemoizedSelector", "DUCKYM005"),
                ("Ducky.Fsa", "DUCKYM006"),
                ("Ducky.Fsa`1", "DUCKYM006"),
                ("Ducky.Fsa`2", "DUCKYM006"),
                ("Ducky.FsaError", "DUCKYM006"),
                ("Ducky.FsaError`1", "DUCKYM006"),
                ("Ducky.FsaMeta`1", "DUCKYM006"),
                ("Ducky.NormalizedState`3", "DUCKYM007"),
                ("Ducky.Blazor.DuckyLayout`1", "DUCKYM008"),
            ],
            ignoreOrder: true);
        tombstones.ShouldAllBe(type =>
            type.IsInterface
            && type.GetMembers().Length == 0
            && type.GetCustomAttribute<ObsoleteAttribute>()!.IsError
            && type.GetCustomAttribute<ObsoleteAttribute>()!.UrlFormat == UrlFormat
            && type.GetCustomAttribute<EditorBrowsableAttribute>()!.State == EditorBrowsableState.Never);
    }
}
