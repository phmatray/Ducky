using System.Collections.Immutable;
using System.Globalization;
using Ducky.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Generators.Tests.Registration;

// DUCKY022 (the "type or method doesn't resolve" branch) and DUCKY023 (SPEC §16.1, §16.2): misuse is reported on the
// type's identifier, never turned into broken generated code, so the output still compiles clean.
public sealed class RegistrationDiagnosticsTests
{
    private const string Prefix = """
        #pragma warning disable CS1591 // justification: undocumented test sources
        namespace App;
        public sealed record Cart(int Count);
        public sealed record Load(int Id);

        """;

    private static ImmutableArray<Diagnostic> Diagnose(Compilation input, out Compilation output)
    {
        input.ShouldCompileClean();
        DiagnosingHarness.Run(new RegistrationGenerator(), input, out output, out var diagnostics);
        output.ShouldCompileClean();
        return diagnostics;
    }

    private static string Generated(Compilation output) =>
        output.SyntaxTrees.Single(t => t.FilePath.EndsWith("DuckyRegistrations.g.cs", StringComparison.Ordinal)).ToString();

    private static void ShouldBeAt(Diagnostic diagnostic, Compilation compilation, string typeName)
    {
        var declared = compilation.GetSymbolsWithName(typeName, SymbolFilter.Type, TestContext.Current.CancellationToken).Single().Locations[0];
        // In the user's tree, so #pragma warning disable and [SuppressMessage] can suppress it.
        diagnostic.Location.IsInSource.ShouldBeTrue();
        diagnostic.Location.SourceTree.ShouldBe(declared.SourceTree);
        diagnostic.Location.SourceSpan.ShouldBe(declared.SourceSpan);
    }

    private const string NoParameterless = "a slice needs a public parameterless constructor that new() can call (one marked [SetsRequiredMembers] when the slice has required members)";
    private const string FileLocal = "it is file-local, so the generated registration can't reference it";

    public static TheoryData<string, string, string> ExcludedShapes => new()
    {
        { "public sealed class S : Ducky.Slice<Cart> { public S(int x) { } protected override Cart Initial => new(0); }", "S", NoParameterless },
        { "public sealed class S : Ducky.Slice<Cart> { internal S() { } protected override Cart Initial => new(0); }", "S", NoParameterless },
        { "public sealed class S : Ducky.Slice<Cart> { public required string Name { get; init; } protected override Cart Initial => new(0); }", "S", NoParameterless },
        { "public sealed class S : Ducky.Slice<Cart> { public required int Count; protected override Cart Initial => new(0); }", "S", NoParameterless },
        { "public abstract class B : Ducky.Slice<Cart> { public required int Count { get; init; } } public sealed class S : B { private int _seen; public int Seen => _seen++; protected override Cart Initial => new(0); }", "S", NoParameterless },
        { "file sealed class S : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }", "S", FileLocal },
        { "file sealed class E : Ducky.EffectGroup { }", "E", FileLocal },
        { "file static class Host { public sealed class S : Ducky.Slice<Cart> { protected override Cart Initial => new(0); } }", "S", FileLocal },
    };

    [Theory]
    [MemberData(nameof(ExcludedShapes))]
    public void Ducky023_ExcludedType_ReportedAndNotRegistered(string source, string typeName, string reason)
    {
        var input = GeneratorHarness.Compile(Prefix + source);
        var diagnostic = Diagnose(input, out var output).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("DUCKY023");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        var name = input.GetSymbolsWithName(typeName, SymbolFilter.Type, TestContext.Current.CancellationToken).Single().ToDisplayString();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe($"'{name}' is not registered by AddDuckyGenerated: {reason}");
        ShouldBeAt(diagnostic, input, typeName);
        Generated(output).ShouldNotContain($".{typeName}>");
    }

    [Fact]
    public void Ducky023_PartialType_ReportedOnce()
    {
        var input = GeneratorHarness.Compile(Prefix + """
            public partial class S : Ducky.Slice<Cart> { public S(int x) { } protected override Cart Initial => new(0); }
            public partial class S : System.IDisposable { public void Dispose() { } }
            """);

        Diagnose(input, out _).ShouldHaveSingleItem().Id.ShouldBe("DUCKY023");
    }

    // Trees sharing a path (the harness gives every tree an empty one): both diagnostics land in the declaring tree,
    // never resolved by path into the first, where the span would fall outside it and crash the generator (INV-25).
    [Fact]
    public void Ducky023_SecondTreeSamePath_ReportedInDeclaringTree()
    {
        var input = GeneratorHarness.Compile("namespace App;", Prefix + """
            [Ducky.Persist] file sealed class S : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            """);
        var diagnostics = Diagnose(input, out _);

        diagnostics.Select(d => d.Id).ShouldBe(["DUCKY023", "DUCKY022"], ignoreOrder: true);
        diagnostics.ShouldAllBe(d => d.Location.SourceTree == input.SyntaxTrees[1]);
        ShouldBeAt(diagnostics.Single(d => d.Id == "DUCKY023"), input, "S");
    }

    // Reported in the user's tree, so #pragma warning disable suppresses it (the compiler then drops it), as for a
    // file slice registered by hand.
    [Fact]
    public void Ducky023_Pragma_Suppressed()
    {
        var input = GeneratorHarness.Compile(Prefix + """
            #pragma warning disable DUCKY023 // justification: registered by hand
            file sealed class S : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            #pragma warning restore DUCKY023
            """);

        Diagnose(input, out _).ShouldHaveSingleItem().IsSuppressed.ShouldBeTrue();
    }

    private const string Attributed = """
        [Ducky.Persist] public sealed class Persisted : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
        [Ducky.Prerender] public sealed class Prerendered : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
        [Ducky.Persist, Ducky.Prerender] public sealed class Both : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
        """;

    // Without a Ducky.Blazor reference neither gated method resolves: DUCKY022 per attribute, nothing is emitted for
    // it, and the slice itself is still registered.
    [Fact]
    public void Ducky022_NoBlazorReference_ReportedPerAttributeAndSliceRegistered()
    {
        var input = GeneratorHarness.Compile(Prefix + Attributed);
        var diagnostics = Diagnose(input, out var output);

        const string Persist = "'Ducky.Blazor.DuckyBlazorBuilderExtensions.PersistAttributeDefaults<TSlice>(DuckyBuilder, Action<PersistOptions>)' does not resolve (reference a Ducky.Blazor version that declares it)";
        const string Prerender = "'Ducky.Blazor.DuckyBlazorBuilderExtensions.Prerender<TSlice>(DuckyBuilder)' does not resolve (reference a Ducky.Blazor version that declares it)";
        diagnostics.Select(d => (d.Id, d.Severity, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe(
        [
            ("DUCKY022", DiagnosticSeverity.Warning, $"[Persist] on 'App.Persisted' is ignored: {Persist}"),
            ("DUCKY022", DiagnosticSeverity.Warning, $"[Prerender] on 'App.Prerendered' is ignored: {Prerender}"),
            ("DUCKY022", DiagnosticSeverity.Warning, $"[Persist] on 'App.Both' is ignored: {Persist}"),
            ("DUCKY022", DiagnosticSeverity.Warning, $"[Prerender] on 'App.Both' is ignored: {Prerender}"),
        ], ignoreOrder: true);
        ShouldBeAt(diagnostics.Single(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("'App.Persisted'", StringComparison.Ordinal)), input, "Persisted");
        ShouldBeAt(diagnostics.Single(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("'App.Prerendered'", StringComparison.Ordinal)), input, "Prerendered");
        Generated(output).ShouldSatisfyAllConditions(
            g => g.ShouldContain("b.AddSlice<global::App.Persisted>();"),
            g => g.ShouldContain("b.AddSlice<global::App.Prerendered>();"),
            g => g.ShouldContain("b.AddSlice<global::App.Both>();"),
            g => g.ShouldNotContain("Blazor"));
    }

    // INV-25, stage 12: against the real Ducky.Blazor both gated methods resolve, so every gated call is emitted, no
    // DUCKY022 is reported and the output compiles (a signature change to either method fails here, never silently turns
    // into DUCKY022). [Persist] carries only the named arguments written, a bare one an empty delegate.
    [Fact]
    public void Registration_RealBlazor_EmitsPersistAndPrerender_NoDucky022()
    {
        var input = DiagnosingHarness.WithBlazor(GeneratorHarness.Compile(Prefix + Attributed + """
            [Ducky.Persist(Version = 2, SyncAcrossTabs = true)] public sealed class Versioned : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            """));
        input.GetTypeByMetadataName("Ducky.Blazor.DuckyBlazorBuilderExtensions").ShouldNotBeNull();

        Diagnose(input, out var output).ShouldBeEmpty();
        Generated(output).ShouldSatisfyAllConditions(
            g => g.ShouldContain("global::Ducky.Blazor.DuckyBlazorBuilderExtensions.PersistAttributeDefaults<global::App.Persisted>(b, o => { });"),
            g => g.ShouldContain("global::Ducky.Blazor.DuckyBlazorBuilderExtensions.PersistAttributeDefaults<global::App.Both>(b, o => { });"),
            g => g.ShouldContain("global::Ducky.Blazor.DuckyBlazorBuilderExtensions.PersistAttributeDefaults<global::App.Versioned>(b, o => { o.Version = 2; o.SyncAcrossTabs = true; });"),
            g => g.ShouldContain("global::Ducky.Blazor.DuckyBlazorBuilderExtensions.Prerender<global::App.Prerendered>(b);"),
            g => g.ShouldContain("global::Ducky.Blazor.DuckyBlazorBuilderExtensions.Prerender<global::App.Both>(b);"),
            g => g.ShouldNotContain("PersistAttributeDefaults<global::App.Prerendered>"));
    }

    // A hand-written older-shape Ducky.Blazor (the skew fixture of SPEC §16.1): the extension type resolves, but neither
    // gated method matches its exact shape, so the attributes still get DUCKY022.
    [Fact]
    public void Ducky022_OlderShapeBlazor_Reported()
    {
        var older = GeneratorHarness.Compile("""
            #pragma warning disable CS1591 // justification: undocumented fixture
            namespace Ducky.Blazor;
            public static class DuckyBlazorBuilderExtensions
            {
                public static Ducky.DuckyBuilder Prerender<TSlice>(this Ducky.DuckyBuilder builder) => builder;
                public static Ducky.DuckyBuilder PersistAttributeDefaults<TSlice>(this Ducky.DuckyBuilder builder) => builder;
                internal static Ducky.DuckyBuilder PersistAttributeDefaults<TSlice>(this Ducky.DuckyBuilder builder, System.Action<object> configure) => builder;
            }
            """).WithAssemblyName("Ducky.Blazor");
        older.ShouldCompileClean();
        var input = GeneratorHarness.Compile(Prefix + Attributed).AddReferences(older.ToMetadataReference());

        // Prerender<TSlice> has its exact shape there, PersistAttributeDefaults<TSlice> doesn't: only [Persist] is reported,
        // and Prerender<T> is emitted against the fixture (the gate is the method symbol, not the real assembly).
        const string Persist = "'Ducky.Blazor.DuckyBlazorBuilderExtensions.PersistAttributeDefaults<TSlice>(DuckyBuilder, Action<PersistOptions>)' does not resolve (reference a Ducky.Blazor version that declares it)";
        Diagnose(input, out var output).Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe(
        [
            ("DUCKY022", $"[Persist] on 'App.Persisted' is ignored: {Persist}"),
            ("DUCKY022", $"[Persist] on 'App.Both' is ignored: {Persist}"),
        ], ignoreOrder: true);
        Generated(output).ShouldSatisfyAllConditions(
            g => g.ShouldContain("global::Ducky.Blazor.DuckyBlazorBuilderExtensions.Prerender<global::App.Prerendered>(b);"),
            g => g.ShouldContain("global::Ducky.Blazor.DuckyBlazorBuilderExtensions.Prerender<global::App.Both>(b);"),
            g => g.ShouldNotContain("Prerender<global::App.Persisted>"),
            g => g.ShouldNotContain("PersistAttributeDefaults<"));
    }

    // The Prerender<T> skew case (stage 11): an older-shape Ducky.Blazor whose Prerender methods all differ from the exact
    // public Prerender<TSlice>(DuckyBuilder) shape (wrong parameters, arity or accessibility). [Prerender] gets DUCKY022,
    // no Prerender<T> call is emitted (no CS0117/CS1501 in generated code), and the slices are still registered.
    [Fact]
    public void Ducky022_OlderShapePrerender_ReportedAndNotEmitted()
    {
        var older = GeneratorHarness.Compile("""
            #pragma warning disable CS1591 // justification: undocumented fixture
            namespace Ducky.Blazor;
            public sealed class PersistOptions { }
            public static class DuckyBlazorBuilderExtensions
            {
                public static Ducky.DuckyBuilder PersistAttributeDefaults<TSlice>(this Ducky.DuckyBuilder builder, System.Action<PersistOptions> configure) => builder;
                public static Ducky.DuckyBuilder Prerender<TSlice>(this Ducky.DuckyBuilder builder, bool include) => builder;
                public static Ducky.DuckyBuilder Prerender<TSlice, TState>(this Ducky.DuckyBuilder builder) => builder;
                internal static Ducky.DuckyBuilder Prerender<TSlice>(this Ducky.DuckyBuilder builder) => builder;
            }
            """).WithAssemblyName("Ducky.Blazor");
        older.ShouldCompileClean();
        var input = GeneratorHarness.Compile(Prefix + Attributed).AddReferences(older.ToMetadataReference());

        const string Prerender = "'Ducky.Blazor.DuckyBlazorBuilderExtensions.Prerender<TSlice>(DuckyBuilder)' does not resolve (reference a Ducky.Blazor version that declares it)";
        var diagnostics = Diagnose(input, out var output);

        diagnostics.Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe(
        [
            ("DUCKY022", $"[Prerender] on 'App.Prerendered' is ignored: {Prerender}"),
            ("DUCKY022", $"[Prerender] on 'App.Both' is ignored: {Prerender}"),
        ], ignoreOrder: true);
        ShouldBeAt(diagnostics.Single(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("'App.Prerendered'", StringComparison.Ordinal)), input, "Prerendered");
        Generated(output).ShouldSatisfyAllConditions(
            g => g.ShouldNotContain(".Prerender<"),
            g => g.ShouldContain("global::Ducky.Blazor.DuckyBlazorBuilderExtensions.PersistAttributeDefaults<global::App.Persisted>(b, o => { });"),
            g => g.ShouldContain("global::Ducky.Blazor.DuckyBlazorBuilderExtensions.PersistAttributeDefaults<global::App.Both>(b, o => { });"),
            g => g.ShouldContain("b.AddSlice<global::App.Prerendered>();"),
            g => g.ShouldContain("b.AddSlice<global::App.Both>();"));
    }

    private const string NotSlice = "it is not a slice";

    // [Persist]/[Prerender] on a non-slice type (SPEC §16.2): reported whether or not Ducky.Blazor is referenced, and an
    // effect carrying it is still registered.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ducky022_NonSlice_Reported(bool blazor)
    {
        var compiled = GeneratorHarness.Compile(Prefix + """
            [Ducky.Persist] public sealed class Effects : Ducky.EffectGroup { }
            [Ducky.Prerender] public sealed class Loader : Ducky.EffectGroup { }
            [Ducky.Prerender] public sealed class Plain { }
            [Ducky.Persist, Ducky.Prerender] public abstract class Base : Ducky.Middleware { }
            """);
        var input = blazor ? DiagnosingHarness.WithBlazor(compiled) : compiled;
        var diagnostics = Diagnose(input, out var output);

        diagnostics.Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe(
        [
            ("DUCKY022", $"[Persist] on 'App.Effects' is ignored: {NotSlice}"),
            ("DUCKY022", $"[Prerender] on 'App.Loader' is ignored: {NotSlice}"),
            ("DUCKY022", $"[Prerender] on 'App.Plain' is ignored: {NotSlice}"),
            ("DUCKY022", $"[Persist] on 'App.Base' is ignored: {NotSlice}"),
            ("DUCKY022", $"[Prerender] on 'App.Base' is ignored: {NotSlice}"),
        ], ignoreOrder: true);
        ShouldBeAt(diagnostics.First(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("'App.Plain'", StringComparison.Ordinal)), input, "Plain");
        Generated(output).ShouldSatisfyAllConditions(
            g => g.ShouldContain("b.AddEffect<global::App.Effects>();"),
            g => g.ShouldContain("b.AddEffect<global::App.Loader>();"),
            g => g.ShouldNotContain("Prerender<"));
    }

    // [Persist]/[Prerender] on a target AttributeUsage forbids (INV-25): CS0592 is the compiler's to report, the generator
    // stays silent and doesn't crash (CS8785), so the rest of the registration is still emitted.
    [Theory]
    [InlineData("[Ducky.Persist] public enum E { A }")]
    [InlineData("[Ducky.Prerender] public delegate void D();")]
    [InlineData("public static class H { [Ducky.Prerender] public static void M() { } }")]
    [InlineData("public static class H { [Ducky.Persist] public static int F; }")]
    public void Ducky022_MisplacedAttribute_NoCrash(string source)
    {
        var input = GeneratorHarness.Compile(Prefix + source + "\npublic sealed class S : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }");
        input.GetDiagnostics(TestContext.Current.CancellationToken).Select(d => d.Id).ShouldContain("CS0592");

        DiagnosingHarness.Run(new RegistrationGenerator(), input, out var output, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        Generated(output).ShouldContain("b.AddSlice<global::App.S>();");
    }

    // Without Ducky.Blazor the attribute is reported on a slice the generator doesn't register too (DUCKY023, abstract).
    [Fact]
    public void Ducky022_UnregisteredSliceNoBlazor_Reported()
    {
        var input = GeneratorHarness.Compile(Prefix + """
            [Ducky.Persist] file sealed class Local : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            [Ducky.Prerender] public abstract class Base : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
            """);

        const string Persist = "'Ducky.Blazor.DuckyBlazorBuilderExtensions.PersistAttributeDefaults<TSlice>(DuckyBuilder, Action<PersistOptions>)' does not resolve (reference a Ducky.Blazor version that declares it)";
        const string Prerender = "'Ducky.Blazor.DuckyBlazorBuilderExtensions.Prerender<TSlice>(DuckyBuilder)' does not resolve (reference a Ducky.Blazor version that declares it)";
        var local = input.GetSymbolsWithName("Local", SymbolFilter.Type, TestContext.Current.CancellationToken).Single().ToDisplayString();
        Diagnose(input, out _).Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe(
        [
            ("DUCKY023", $"'{local}' is not registered by AddDuckyGenerated: {FileLocal}"),
            ("DUCKY022", $"[Persist] on '{local}' is ignored: {Persist}"),
            ("DUCKY022", $"[Prerender] on 'App.Base' is ignored: {Prerender}"),
        ], ignoreOrder: true);
    }

    // The generated method is reachable through extension syntax from the assembly, under the sanitized assembly name.
    [Fact]
    public void Registration_SanitizedAssemblyName_CallableThroughExtensionSyntax()
    {
        var input = GeneratorHarness.Compile(Prefix + "public sealed class S : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }")
            .WithAssemblyName("My.App-2");
        GeneratorHarness.Run(new RegistrationGenerator(), input, out var output);

        Generated(output).ShouldContain("public static class My_App_2_DuckyRegistrations");
        output.AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            using Ducky.Generated;

            internal static class Startup
            {
                public static Ducky.DuckyBuilder Configure(Ducky.DuckyBuilder b) => b.AddDuckyGenerated_My_App_2();
            }
            """, GeneratorHarness.ParseOptions, cancellationToken: TestContext.Current.CancellationToken)).ShouldCompileClean();
    }
}
