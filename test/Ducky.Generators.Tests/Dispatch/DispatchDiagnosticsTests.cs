using System.Collections.Immutable;
using System.Globalization;
using Ducky.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Tests.Dispatch;

// DUCKY020, DUCKY021 and DUCKY024 (SPEC §16.1, §16.2): misuse is reported on the action's identifier, never turned into
// broken generated code, so the output still compiles clean.
public sealed class DispatchDiagnosticsTests
{
    private const string Prefix = "#pragma warning disable CS1591 // justification: undocumented test sources\n";

    // The real Ducky.Testing.dll, copied next to the tests (GEN-06): the TestStore arm of DUCKY021 checks its members.
    private static readonly MetadataReference _testing = MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "Ducky.Testing.dll"));

    private static ImmutableArray<Diagnostic> Diagnose(string source, out Compilation input, out Compilation output, bool withTesting = false)
    {
        // A source without its own namespace declarations goes into namespace App.
        input = GeneratorHarness.Compile(Prefix + (source.StartsWith("namespace", StringComparison.Ordinal) ? source : "namespace App;\n" + source));
        if (withTesting)
        {
            input = input.AddReferences(_testing);
        }

        input.ShouldCompileClean();
        DiagnosingHarness.Run(new DispatchExtensionsGenerator(), input, out output, out var diagnostics);
        output.ShouldCompileClean();
        return diagnostics;
    }

    private static void ShouldBeAt(Diagnostic diagnostic, Compilation compilation, string typeName)
    {
        var declared = compilation.GetSymbolsWithName(typeName, SymbolFilter.Type, TestContext.Current.CancellationToken).Single().Locations.Single();
        diagnostic.Location.SourceSpan.ShouldBe(declared.SourceSpan);
        diagnostic.Location.GetLineSpan().Span.ShouldBe(declared.GetLineSpan().Span);
    }

    private static string Message(Diagnostic diagnostic) => diagnostic.GetMessage(CultureInfo.InvariantCulture);

    public static TheoryData<string, string, string> InvalidShapes => new()
    {
        { "[Ducky.DuckyAction] public sealed record Box<T>(T Value);", "Box", "'App.Box<T>' gets no dispatch helper: it is generic or nested in a generic type" },
        { "public class Host<T> { [Ducky.DuckyAction] public sealed record Item; }", "Item", "'App.Host<T>.Item' gets no dispatch helper: it is generic or nested in a generic type" },
        { "public class Host { [Ducky.DuckyAction] private sealed record Item; }", "Item", "'App.Host.Item' gets no dispatch helper: it is file-local or not accessible from its namespace" },
        { "public class Host { [Ducky.DuckyAction] protected sealed record Item; }", "Item", "'App.Host.Item' gets no dispatch helper: it is file-local or not accessible from its namespace" },
        { "public class Host { [Ducky.DuckyAction] private protected sealed record Item; }", "Item", "'App.Host.Item' gets no dispatch helper: it is file-local or not accessible from its namespace" },
        { "file static class Host { [Ducky.DuckyAction] public sealed record Item; }", "Item", "'App.Host.Item' gets no dispatch helper: it is file-local or not accessible from its namespace" },
        { "[Ducky.DuckyAction, System.Obsolete(\"gone\", true)] public sealed record Gone;", "Gone", "'App.Gone' gets no dispatch helper: it is marked [Obsolete(error: true)]" },
        { "[Ducky.DuckyAction] public abstract record Base(int X);", "Base", "'App.Base' gets no dispatch helper: it has no accessible constructor that is usable with new, not [Obsolete(error: true)], takes no error-obsolete parameter type and sets every required member" },
        { "[Ducky.DuckyAction] public ref struct Frame { }", "Frame", "'App.Frame' gets no dispatch helper: it has no accessible constructor that is usable with new, not [Obsolete(error: true)], takes no error-obsolete parameter type and sets every required member" },
        { "[Ducky.DuckyAction] public static class Holder { }", "Holder", "'App.Holder' gets no dispatch helper: it has no accessible constructor that is usable with new, not [Obsolete(error: true)], takes no error-obsolete parameter type and sets every required member" },
        { "[Ducky.DuckyAction] public sealed record Single { private Single() { } }", "Single", "'App.Single' gets no dispatch helper: it has no accessible constructor that is usable with new, not [Obsolete(error: true)], takes no error-obsolete parameter type and sets every required member" },
        { "[Ducky.DuckyAction] public struct Point { private Point(int x) { } }", "Point", "'App.Point' gets no dispatch helper: it has no accessible constructor that is usable with new, not [Obsolete(error: true)], takes no error-obsolete parameter type and sets every required member" },
        { "[Ducky.DuckyAction(MethodName = \"go-now\")] public sealed record Go;", "Go", "'App.Go' gets no dispatch helper: MethodName 'go-now' is not a valid identifier" },
        { "[Ducky.DuckyAction(MethodName = \"\")] public sealed record Go;", "Go", "'App.Go' gets no dispatch helper: MethodName '' is not a valid identifier" },
        { "[Ducky.DuckyAction] public sealed record Save; public static class SaveDispatchExtensions { }", "Save", "'App.Save' gets no dispatch helper: a type or namespace named 'SaveDispatchExtensions' is already declared in its namespace" },
        { "[Ducky.DuckyAction(MethodName = \"IncDispatchExtensions\")] public sealed record Inc;", "Inc", "'App.Inc' gets no dispatch helper: MethodName 'IncDispatchExtensions' is the name of the generated class" },
        { "[Ducky.DuckyAction] public sealed record Rename { public required string Name { get; init; } }", "Rename", "'App.Rename' gets no dispatch helper: it has no accessible constructor that is usable with new, not [Obsolete(error: true)], takes no error-obsolete parameter type and sets every required member" },
        { "namespace App { [Ducky.DuckyAction] public sealed record Save; namespace SaveDispatchExtensions { } }", "Save", "'App.Save' gets no dispatch helper: a type or namespace named 'SaveDispatchExtensions' is already declared in its namespace" },
    };

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public void Ducky020_InvalidAction_ReportedAndNothingEmitted(string source, string typeName, string message)
    {
        var diagnostic = Diagnose(source, out var input, out var output).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("DUCKY020");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        Message(diagnostic).ShouldBe(message);
        ShouldBeAt(diagnostic, input, typeName);
        output.SyntaxTrees.Count().ShouldBe(input.SyntaxTrees.Count());
    }

    // Joined class names coincide (A.B_C and A_B.C both give A_B_CDispatchExtensions): both are reported, neither emitted.
    [Fact]
    public void Ducky020_JoinedClassNamesCoincide_BothReported()
    {
        var diagnostics = Diagnose("""
            public static class A { [Ducky.DuckyAction] public sealed record B_C; }
            public static class A_B { [Ducky.DuckyAction] public sealed record C; }
            [Ducky.DuckyAction] public sealed record Fine;
            """, out var input, out var output);

        diagnostics.Select(Message).ShouldBe([
            "'App.A.B_C' gets no dispatch helper: another [DuckyAction] type generates the class 'A_B_CDispatchExtensions'",
            "'App.A_B.C' gets no dispatch helper: another [DuckyAction] type generates the class 'A_B_CDispatchExtensions'",
        ]);
        diagnostics.ShouldAllBe(d => d.Id == "DUCKY020");
        ShouldBeAt(diagnostics[0], input, "B_C");
        ShouldBeAt(diagnostics[1], input, "C");
        output.SyntaxTrees.Count().ShouldBe(input.SyntaxTrees.Count() + 1);
    }

    // C# binds an accessible instance member before an extension method: `record Dispose` would make
    // store.DisposeAsync() dispose the store.
    [Theory]
    [InlineData("[Ducky.DuckyAction] public sealed record Dispose;", "Dispose", "Dispose")]
    [InlineData("[Ducky.DuckyAction] public sealed record Initialize;", "Initialize", "Initialize")]
    [InlineData("[Ducky.DuckyAction] public sealed record WhenIdle;", "WhenIdle", "WhenIdle")]
    [InlineData("[Ducky.DuckyAction] public sealed record Dispatch(int X);", "Dispatch", "Dispatch")]
    [InlineData("[Ducky.DuckyAction] public sealed record Trigger;", "Trigger", "Trigger")]
    [InlineData("[Ducky.DuckyAction] public sealed record Slices;", "Slices", "Slices")]
    [InlineData("[Ducky.DuckyAction(MethodName = \"Equals\")] public sealed record Compare;", "Compare", "Equals")]
    public void Ducky021_HelperNameCollides_SkippedWithWarning(string source, string typeName, string methodName)
    {
        var diagnostic = Diagnose(source, out var input, out var output).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("DUCKY021");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        Message(diagnostic).ShouldBe(
            $"The dispatch helpers of 'App.{typeName}' are skipped: '{methodName}' or '{methodName}Async' is a member of IDispatcher, IStore, EffectContext, TestStore or object, which C# binds before an extension method; set MethodName");
        ShouldBeAt(diagnostic, input, typeName);
        output.SyntaxTrees.Count().ShouldBe(input.SyntaxTrees.Count());
    }

    // Non-normative (M9-04c): with Ducky.Testing referenced, TestStore's accessible instance members are reserved too,
    // so `record Settled` can't make testStore.Settled() skip the dispatch. Every row names a member only TestStore has;
    // property names bind first as well.
    [Theory]
    [InlineData("[Ducky.DuckyAction] public sealed record Settled;", "Settled", "Settled")]
    [InlineData("[Ducky.DuckyAction] public sealed record Seed(int X);", "Seed", "Seed")]
    [InlineData("[Ducky.DuckyAction] public sealed record GetSlice;", "GetSlice", "GetSlice")]
    [InlineData("[Ducky.DuckyAction] public sealed record Failures;", "Failures", "Failures")]
    [InlineData("[Ducky.DuckyAction(MethodName = \"Processed\")] public sealed record Done;", "Done", "Processed")]
    public void Ducky021_TestStoreMemberCollides_SkippedWithWarning(string source, string typeName, string methodName)
    {
        var diagnostic = Diagnose(source, out var input, out var output, withTesting: true).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("DUCKY021");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        Message(diagnostic).ShouldBe(
            $"The dispatch helpers of 'App.{typeName}' are skipped: '{methodName}' or '{methodName}Async' is a member of IDispatcher, IStore, EffectContext, TestStore or object, which C# binds before an extension method; set MethodName");
        ShouldBeAt(diagnostic, input, typeName);
        output.SyntaxTrees.Count().ShouldBe(input.SyntaxTrees.Count());
    }

    // Non-normative (M9-04c): without Ducky.Testing there is no TestStore to collide with; even when it is referenced,
    // its private BoundAsync can't hide a helper, nor can its static Create: C# drops static candidates for an instance
    // receiver, so testStore.Create() binds to the extension.
    [Theory]
    [InlineData("[Ducky.DuckyAction] public sealed record Settled;", false)]
    [InlineData("[Ducky.DuckyAction] public sealed record Bound;", true)]
    [InlineData("[Ducky.DuckyAction] public sealed record Create;", true)]
    public void Ducky021_TestStoreArm_OnlyAccessibleInstanceMembersOfAReferencedTestStore(string source, bool withTesting)
    {
        Diagnose(source, out var input, out var output, withTesting).ShouldBeEmpty();

        output.SyntaxTrees.Count().ShouldBe(input.SyntaxTrees.Count() + 1);
    }

    // Importing both namespaces (as _Imports.razor or global usings do) makes every call CS0121.
    [Fact]
    public void Ducky024_SameNameAndParameterTypes_ReportedOnTheLaterAction()
    {
        var diagnostics = Diagnose("""
            namespace App { public sealed class A { } public sealed class Aint { } }
            namespace App.One
            {
                [Ducky.DuckyAction] public sealed record Increment(int By);
                [Ducky.DuckyAction] public sealed record Load(string Slug) { public Load(bool flag) : this(flag.ToString()) { } }
                [Ducky.DuckyAction] public sealed record Wide(int X);
                [Ducky.DuckyAction] public sealed record Spread(App.A A, int B);
                [Ducky.DuckyAction] public sealed record Named(int X);
            }
            namespace App.Two
            {
                [Ducky.DuckyAction] public sealed record Increment(int Step);
                [Ducky.DuckyAction] public sealed record Load(int Id) { public Load(string? slug) : this(slug!.Length) { } }
                [Ducky.DuckyAction] public sealed record Wide(long X);
                [Ducky.DuckyAction] public sealed record Spread(App.Aint A);
                [Ducky.DuckyAction(MethodName = "NamedTwo")] public sealed record Named(int X);
            }
            """, out var input, out var output);

        diagnostics.Select(Message).ShouldBe([
            "The dispatch helper 'Increment' of 'App.Two.Increment' has the same name and parameter types as the one of 'App.One.Increment': a call that sees both is ambiguous (CS0121); set MethodName on one of them",
            "The dispatch helper 'Load' of 'App.Two.Load' has the same name and parameter types as the one of 'App.One.Load': a call that sees both is ambiguous (CS0121); set MethodName on one of them",
        ]);
        diagnostics.ShouldAllBe(d => d.Id == "DUCKY024" && d.Severity == DiagnosticSeverity.Info);
        diagnostics[0].Location.SourceSpan.ShouldBe(input.GetTypeByMetadataName("App.Two.Increment")!.Locations.Single().SourceSpan);
        diagnostics[1].Location.SourceSpan.ShouldBe(input.GetTypeByMetadataName("App.Two.Load")!.Locations.Single().SourceSpan);
        output.SyntaxTrees.Count().ShouldBe(input.SyntaxTrees.Count() + 10);
    }

    // Tuple element names are not part of the signature: (int A, string B) and (int X, string Y) are both CS0121.
    [Fact]
    public void Ducky024_TupleNamesDiffer_Reported()
    {
        var diagnostic = Diagnose("""
            namespace App.One { [Ducky.DuckyAction] public sealed record Pair((int A, string B) Value); }
            namespace App.Two { [Ducky.DuckyAction] public sealed record Pair((int X, string Y) Value); }
            """, out _, out _).ShouldHaveSingleItem();

        Message(diagnostic).ShouldBe("The dispatch helper 'Pair' of 'App.Two.Pair' has the same name and parameter types as the one of 'App.One.Pair': a call that sees both is ambiguous (CS0121); set MethodName on one of them");
    }

    // Also inside a containing type's type arguments: Outer<(int A, string B)>.Inner and Outer<(int X, string Y)>.Inner.
    [Fact]
    public void Ducky024_TupleNamesInContainingTypeDiffer_Reported()
    {
        var diagnostic = Diagnose("""
            namespace App { public class Outer<T> { public sealed class Inner { } } }
            namespace App.One { [Ducky.DuckyAction] public sealed record Pair(App.Outer<(int A, string B)>.Inner Value); }
            namespace App.Two { [Ducky.DuckyAction] public sealed record Pair(App.Outer<(int X, string Y)>.Inner Value); }
            """, out _, out _).ShouldHaveSingleItem();

        Message(diagnostic).ShouldBe("The dispatch helper 'Pair' of 'App.Two.Pair' has the same name and parameter types as the one of 'App.One.Pair': a call that sees both is ambiguous (CS0121); set MethodName on one of them");
    }

    // Only accessible members are reserved: object's protected MemberwiseClone can't hide a helper.
    [Fact]
    public void Ducky021_ProtectedObjectMember_NotReserved()
    {
        Diagnose("[Ducky.DuckyAction] public sealed record MemberwiseClone;", out var input, out var output).ShouldBeEmpty();

        output.SyntaxTrees.Count().ShouldBe(input.SyntaxTrees.Count() + 1);
    }

    // Non-normative: same namespace, two actions sharing a MethodName: every call is CS0121 even without imports.
    [Fact]
    public void Ducky024_SameNamespaceSharedMethodName_Reported()
    {
        var diagnostic = Diagnose("""
            [Ducky.DuckyAction(MethodName = "Go")] public sealed record First(int X);
            [Ducky.DuckyAction(MethodName = "Go")] public sealed record Second(int Y);
            """, out _, out _).ShouldHaveSingleItem();

        Message(diagnostic).ShouldBe("The dispatch helper 'Go' of 'App.Second' has the same name and parameter types as the one of 'App.First': a call that sees both is ambiguous (CS0121); set MethodName on one of them");
    }

    // Each action emits M and MAsync: Load's async helper and LoadAsync's sync helper are both LoadAsync(this IDispatcher, int).
    [Fact]
    public void Ducky024_AsyncHelperMatchesOtherBaseName_Reported()
    {
        var diagnostic = Diagnose("""
            namespace App.One { [Ducky.DuckyAction] public sealed record Load(int Id); }
            namespace App.Two { [Ducky.DuckyAction] public sealed record LoadAsync(int Id); }
            """, out var input, out _).ShouldHaveSingleItem();

        Message(diagnostic).ShouldBe("The dispatch helper 'LoadAsync' of 'App.Two.LoadAsync' has the same name and parameter types as the one of 'App.One.Load': a call that sees both is ambiguous (CS0121); set MethodName on one of them");
        diagnostic.Location.SourceSpan.ShouldBe(input.GetTypeByMetadataName("App.Two.LoadAsync")!.Locations.Single().SourceSpan);
    }

    // The same collision through MethodName, with the Async-named action first and both in one namespace.
    [Fact]
    public void Ducky024_MethodNameMatchesOtherAsyncName_Reported()
    {
        var diagnostic = Diagnose("""
            [Ducky.DuckyAction(MethodName = "GoAsync")] public sealed record First(int X);
            [Ducky.DuckyAction(MethodName = "Go")] public sealed record Second(int Y);
            """, out _, out _).ShouldHaveSingleItem();

        Message(diagnostic).ShouldBe("The dispatch helper 'GoAsync' of 'App.Second' has the same name and parameter types as the one of 'App.First': a call that sees both is ambiguous (CS0121); set MethodName on one of them");
    }

    // Matching names alone are not ambiguous: Load(int) and LoadAsync(string) overload cleanly.
    [Fact]
    public void Ducky024_AsyncNameDifferentParameterTypes_NotReported()
    {
        Diagnose("""
            namespace App.One { [Ducky.DuckyAction] public sealed record Load(int Id); }
            namespace App.Two { [Ducky.DuckyAction] public sealed record LoadAsync(string Slug); }
            """, out _, out _).ShouldBeEmpty();
    }

    // A parameter type that doesn't resolve yet (CS0246 while typing) is neither a generator crash (CS8785, INV-25) nor
    // reported again: its constructor gets no helper, so no compiler error points into generated code.
    [Theory]
    [InlineData("public class Foo { } [Ducky.DuckyAction] public sealed record R(Foo.Bar X);")]
    [InlineData("public class Foo<T> { } [Ducky.DuckyAction] public sealed record R(Foo<(int A, int B)>.Bar X);")]
    [InlineData("[Ducky.DuckyAction] public sealed record R(Missing<int> X);")]
    [InlineData("[Ducky.DuckyAction] public sealed record R(Missing[] X);")]
    public void UnresolvedParameterType_NoCrashAndNoErrorInGeneratedCode(string source)
    {
        var input = GeneratorHarness.Compile(Prefix + "namespace App;\n" + source);

        DiagnosingHarness.Run(new DispatchExtensionsGenerator(), input, out var output, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Location.SourceTree is not null && !input.SyntaxTrees.Contains(d.Location.SourceTree))
            .ShouldBeEmpty();
    }

    // [DuckyAction] misapplied (CS0592) to a delegate, a method or an enum is ignored, not a generator crash (CS8785).
    [Fact]
    public void MisappliedAttribute_Ignored()
    {
        var input = GeneratorHarness.Compile(Prefix + """
            namespace App;
            [Ducky.DuckyAction] public delegate void Callback();
            [Ducky.DuckyAction] public enum Mode { On }
            public static class Host { [Ducky.DuckyAction] public static void Run() { } }
            [Ducky.DuckyAction] public sealed record Fine;
            """);

        DiagnosingHarness.Run(new DispatchExtensionsGenerator(), input, out var output, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        output.SyntaxTrees.Count().ShouldBe(input.SyntaxTrees.Length + 1);
    }
}
