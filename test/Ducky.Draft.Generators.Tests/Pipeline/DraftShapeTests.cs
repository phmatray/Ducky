using System.Globalization;
using Ducky.Draft.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Ducky.Draft.Generators.Tests.Pipeline;

// Draft shape matrix, scalar and other plain members (SPEC §13.3, §17.7, INV-25): every shape's output compiles without
// any diagnostic under DocumentationMode.Diagnose, nullable enabled and warning level 9999, and its generated sources and
// generator diagnostics are snapshotted. Shape sources skip their own docs (CS1591); generated code gets no such pass.
public sealed class DraftShapeTests
{
    private const string Undocumented = "#pragma warning disable CS1591 // justification: shape sources are undocumented; only generated code is checked\n";

    private static readonly Dictionary<string, string> _shapes = new()
    {
        ["Scalars"] = """
            namespace App;
            public enum Filter { All, Done }
            [Ducky.Draft.Draftable]
            public sealed partial record Todo(int Id, string Title, bool Done, double? Score, Filter Filter, decimal Price, System.Guid Key, string? Note)
            {
                public System.DateTime Due { get; set; }
                public required int Rank { get; init; }
                public int Length => Title.Length;
                public string Slug { get; } = "";
                public int Hidden { get; private init; }
                public int Counted { get; internal set; }
                public (string Name, int Age) Owner { get; init; }
                public System.Collections.Generic.List<string> Tags { get; init; } = [];
                public string[] Lines { get; init; } = [];
                public dynamic Any { get; init; } = 0;
            }
            """,
        ["Empty"] = """
            namespace App;
            [Ducky.Draft.Draftable] public partial record Empty;
            [Ducky.Draft.Draftable] public abstract partial record Shape(int Sides) { public abstract double Area { get; init; } }
            """,
        // IDraft members are explicit: members named like them are ordinary draft properties; Build() is omitted.
        ["DraftApiNames"] = """
            namespace App;
            [Ducky.Draft.Draftable] public sealed partial record Job(int Build, bool IsDirty, string Revoke);
            [Ducky.Draft.Draftable] public sealed partial record Task(int Id) { public void Build() { } }
            """,
        // Private names a naive `__` + camelCase scheme would duplicate (CS0102). M10-05 must change `Tags` to
        // ImmutableList<string>, so `__s_Tags` (its assigned flag) sits next to `__m_TagsSet`.
        ["PrivateNames"] = """
            namespace App;
            [Ducky.Draft.Draftable] public sealed partial record Session(string Id, bool Revoked, int O, string original);
            [Ducky.Draft.Draftable] public sealed partial record Labels(string Tags, string TagsSet);
            [Ducky.Draft.Draftable] public sealed partial record Cased(int Items, int items, string value, string recipe, string draft);
            """,
        // Members named with a private prefix of generated code are left out of the draft (`with` keeps their value).
        ["ReservedNames"] = """
            namespace App;
            [Ducky.Draft.Draftable] public sealed partial record R(int X, int __m_X, int __x_original, int __s_X);
            """,
        // Generated code writes string literals, never nameof: a member or method named nameof would rebind it.
        ["NameOf"] = """
            namespace App;
            [Ducky.Draft.Draftable] public sealed partial record Named(int nameof, string Title);
            [Ducky.Draft.Draftable] public sealed partial record Shadowing(int X) { private static string nameof(object value) => $"{value}"; }
            """,
        ["Accessibility"] = """
            namespace App;
            internal sealed class Secret { }
            [Ducky.Draft.Draftable]
            public partial record Account(int Id)
            {
                private int Pin { get; init; }
                protected int Salt { get; init; }
                internal Secret? Key { get; init; }
                protected internal int Level { get; init; }
                private protected int Grade { get; init; }
                public int Visible { get; protected internal set; }
                public int Tier { internal get; init; }
                public static int Count { get; set; }
                public int this[int i] => i;
                public int Field;
                public int Pinned => Pin;
            }
            [Ducky.Draft.Draftable] internal sealed partial record Hidden(Secret Value, int X);
            public partial class Host { [Ducky.Draft.Draftable] private sealed partial record Inner(int X); }
            """,
        // A property named like an object member hides it in the draft too: `new`, no CS0108.
        ["ObjectNames"] = """
            namespace App;
            [Ducky.Draft.Draftable]
            public sealed partial record Mirror
            {
                public new int GetType { get; init; }
                public new string MemberwiseClone { get; init; } = "";
                public new bool ReferenceEquals { get; init; }
            }
            """,
        ["Obsolete"] = """
            using System.Diagnostics.CodeAnalysis;
            namespace App;
            [System.Obsolete("old")] public sealed class Legacy { }
            [System.Obsolete("gone", error: true)] public sealed class Gone { }
            [Experimental("APPX")] public sealed class Preview { }
            [System.Obsolete("old", DiagnosticId = "APP002")] public sealed class Ancient { }
            [Ducky.Draft.Draftable, System.Obsolete("old")] public sealed partial record Retired(int X);
            [Ducky.Draft.Draftable]
            public sealed partial record Mixed
            {
                [System.Obsolete("old")] public int Old { get; init; }
                [System.Obsolete("gone", error: true)] public int Removed { get; init; }
                [System.Obsolete("old")] public Legacy? Kept { get; init; }
                [System.Obsolete("old")] public Gone? Lost { get; init; }
                [System.Obsolete("old", DiagnosticId = "APP001")] public int Tagged { get; init; }
                [Experimental("APPY")] public int Trial { get; init; }
                [Experimental("APPX")] public Preview? Peek { get; init; }
                [System.Obsolete("old")] public System.Collections.Generic.List<Ancient>? Antique { get; init; }
                public int GetGone { [System.Obsolete("gone", true)] get; init; }
                public int InitGone { get; [System.Obsolete("gone", true)] init; }
                public int AccessorTagged { [System.Obsolete("old", DiagnosticId = "APP003")] get; [System.Obsolete("old", DiagnosticId = "APP004")] init; }
                [System.Obsolete("old", DiagnosticId = "")] public int BlankId { get; init; }
                // An id a #pragma can't name leaves its member out (the compiler already rejects such an [Experimental] id, CS9211).
                [System.Obsolete("old", DiagnosticId = "APP 8")] public int Spaced { get; init; }
            }
            [Ducky.Draft.Draftable, System.Obsolete("gone", true)] public sealed partial record Dead(int X);
            // C# binds a member access on an override to the original virtual declaration, so its [Obsolete] and
            // [Experimental] apply to generated code even when the override is plainly obsolete (the CS0672 fix) or bare.
            public abstract record Base
            {
                [System.Obsolete("gone", true)] public virtual int Erased { get; init; }
                [System.Obsolete("old", DiagnosticId = "APP005")] public virtual int Flagged { get; init; }
                public virtual int GetErased { [System.Obsolete("gone", true)] get; init; }
                public virtual int InitErased { get; [System.Obsolete("gone", true)] init; }
                public virtual int InitFlagged { get; [System.Obsolete("old", DiagnosticId = "APP006")] init; }
                [Experimental("APPZ")] public virtual int Tried { get; init; }
            }
            [Ducky.Draft.Draftable]
            public sealed partial record Heir : Base
            {
                [System.Obsolete("old")] public override int Erased { get; init; }
                [System.Obsolete("old")] public override int Flagged { get; init; }
                public override int GetErased { [System.Obsolete("old")] get; init; }
                public override int InitErased { get; [System.Obsolete("old")] init; }
                public override int InitFlagged { get; [System.Obsolete("old")] init; }
                public override int Tried { get; init; }
            }
            """,
        // GEN-06: oblivious reference types (user source under #nullable disable) are emitted annotated, except where a
        // type parameter can't be nullable; tuple element names survive; an inherited List<T> of an oblivious T keeps
        // its declared outer annotation.
        ["NullableDisabled"] = """
            using System.Collections.Generic;
            namespace App;
            public sealed class Child { }
            public abstract record Holder<T>(List<T> Items) { public T[] Values { get; init; } = []; }
            public sealed class Ref<T> where T : class { }
            public class Animal { }
            public sealed class Typed<T> where T : Animal { }
            #nullable disable
            [Ducky.Draft.Draftable] public sealed partial record Strings(string Extra) : Holder<string>(new List<string>());
            [Ducky.Draft.Draftable]
            public sealed partial record Legacy(
                string Name, Child Child, List<string> Names, string[] Lines, int[] Counts, Dictionary<string, object> Bag,
                (string Name, int Age) Owner, (int, Child Named) Partial, int? Maybe, List<(string K, int V)> Pairs, Ref<string> Strict, Typed<Animal> Bound,
                System.ValueTuple<string> One,
                Child Missing = null);
            """,
        // Hint-name row: same-named records in two namespaces.
        ["SameName"] = """
            namespace A { [Ducky.Draft.Draftable] public sealed partial record Todo(int Id); }
            namespace B { [Ducky.Draft.Draftable] public sealed partial record Todo(int Id); }
            """,
        ["Nesting"] = """
            namespace App
            {
                public static partial class Outer
                {
                    public partial struct Middle
                    {
                        [Ducky.Draft.Draftable] public sealed partial record Leaf(int X);
                    }
                    public partial record Holder { [Ducky.Draft.Draftable] public partial record Item(string Y); }
                    public partial interface IHost { [Ducky.Draft.Draftable] public sealed partial record Inner(int Z); }
                    public partial record struct Pair { [Ducky.Draft.Draftable] public sealed partial record Inside(int W); }
                }
            }
            [Ducky.Draft.Draftable] public sealed partial record Global(int G);
            """,
        // A lower-case ASCII type name is declared verbatim in generated code (no second CS8981).
        ["Keywords"] = """
            namespace App;
            [Ducky.Draft.Draftable] public sealed partial record @class(int @event, string @int);
            #pragma warning disable CS8981 // justification: the lower-case name is the shape under test
            [Ducky.Draft.Draftable] public sealed partial record zap(int Volts);
            """,
        // Inherited properties are mirrored, the most derived one when hidden; a ref struct member is get-only.
        ["Derived"] = """
            namespace App;
            public abstract record Base(int Id, string Name) { public int Shadowed { get; init; } }
            [Ducky.Draft.Draftable]
            public sealed partial record Derived(int Id, string Name, bool Flag) : Base(Id, Name)
            {
                public new string Shadowed { get; init; } = "";
                internal System.Span<int> Window { get => default; init { } }
            }
            """,
        // Nullable flow attributes on the property or its accessors: MaybeNull reads may be null, DisallowNull writes may not.
        ["NullableAttributes"] = """
            namespace App;
            using System.Diagnostics.CodeAnalysis;
            [Ducky.Draft.Draftable]
            public sealed partial record Contact
            {
                [MaybeNull] public string Name { get; init; } = "";
                public string Alias { [return: MaybeNull] get; init; } = "";
                [MaybeNull] public string Code { get; } = "";
                [MaybeNull] public int Count { get; init; }
                [DisallowNull] public string? Email { get; init; }
                public string? Phone { get; [param: DisallowNull] init; }
                [AllowNull] public string Nick { get; init; } = "";
                [NotNull] public string? City { get; init; } = "";
            }
            """,
    };

    public static TheoryData<string> Shapes => [.. _shapes.Keys];

    [Theory]
    [MemberData(nameof(Shapes))]
    public Task Generated_CompilesClean_DraftShape(string shape) => VerifyShape(GeneratorHarness.Compile(Undocumented + _shapes[shape]), shape);

    // A base record in another assembly: its internal members aren't accessible, so they are not mirrored, and neither is
    // a member whose type isn't accessible within the assembly (a protected internal nested type of the base).
    [Fact]
    public Task Generated_CompilesClean_DraftShapeOtherAssemblyBase()
    {
        var lib = GeneratorHarness.Compile(Undocumented + """
            namespace Lib;
            public abstract record Base(int Id)
            {
                internal int Secret { get; init; }
                protected internal int Level { get; init; }
                protected internal sealed class Tag { }
                protected internal Tag? Label { get; init; }
                public int Open { get; internal init; }
            }
            """).WithAssemblyName("Lib");
        lib.ShouldCompileClean();
        var app = GeneratorHarness.Compile(Undocumented + """
            namespace App;
            [Ducky.Draft.Draftable] public sealed partial record Derived(int Id, string Name) : Lib.Base(Id);
            """).AddReferences(lib.ToMetadataReference());
        return VerifyShape(app, "OtherAssemblyBase");
    }

    // [Experimental] ids of referenced assemblies: a polyfilled attribute, matched by full name as the compiler does, and
    // assembly- and module-level attributes, which apply to every type and member the assembly declares.
    [Fact]
    public Task Generated_CompilesClean_DraftShapeExperimentalLibs()
    {
        var poly = GeneratorHarness.Compile(Undocumented + """
            #pragma warning disable CS0436 // justification: the polyfill under test conflicts with the framework's attribute
            [assembly: System.Diagnostics.CodeAnalysis.Experimental("LIBQ")]
            [module: System.Diagnostics.CodeAnalysis.Experimental("LIBR")]
            namespace System.Diagnostics.CodeAnalysis
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                internal sealed class ExperimentalAttribute(string diagnosticId) : System.Attribute { public string DiagnosticId { get; } = diagnosticId; }
            }
            namespace Poly { [System.Diagnostics.CodeAnalysis.Experimental("LIBP")] public sealed class Probe { } }
            """).WithAssemblyName("Poly");
        poly.ShouldCompileClean();
        var gated = GeneratorHarness.Compile(Undocumented + """
            [assembly: System.Diagnostics.CodeAnalysis.Experimental("LIBA")]
            [module: System.Diagnostics.CodeAnalysis.Experimental("LIBM")]
            namespace Gated;
            public abstract record Base { public int Level { get; init; } }
            """).WithAssemblyName("Gated");
        gated.ShouldCompileClean();
        var app = GeneratorHarness.Compile(Undocumented + """
            #pragma warning disable LIBP, LIBQ, LIBR, LIBA, LIBM // justification: the experimental APIs are the shape under test
            namespace App;
            [Ducky.Draft.Draftable] public sealed partial record Lab(Poly.Probe? Probe, int X) : Gated.Base;
            """).AddReferences(poly.ToMetadataReference(), gated.ToMetadataReference());
        return VerifyShape(app, "ExperimentalLibs");
    }

    // A pointer or function pointer member, also as an array element, is left out: generated code is not unsafe.
    [Fact]
    public Task Generated_CompilesClean_DraftShapeUnsafe()
    {
        var app = GeneratorHarness.Compile(Undocumented + """
            namespace App;
            [Ducky.Draft.Draftable]
            public sealed unsafe partial record Raw(int X)
            {
                public int* Pointer { get => null; init { } }
                public int*[] Pointers { get => new int*[0]; init { } }
                public delegate*<void> Function { get => null; init { } }
                public Outer<int*[]>.Inner? Nested { get => null; init { } }
            }

            public class Outer<T> { public class Inner { } }
            """);
        return VerifyShape(app.WithOptions(app.Options.WithAllowUnsafe(true)), "Unsafe");
    }

    // A member whose type doesn't bind is left out, so generated code adds no diagnostic to the user's own.
    [Fact]
    public void Generated_ErrorTypeMember_AddsNoDiagnostics()
    {
        var input = GeneratorHarness.Compile(Undocumented + """
            namespace App;
            [Ducky.Draft.Draftable]
            public sealed partial record Broken(int X, Missing Ghost) { public System.Collections.Generic.List<Absent>? Ghosts { get; init; } }
            """);
        var expected = input.GetDiagnostics(TestContext.Current.CancellationToken).Select(d => d.ToString()).ToList();
        expected.ShouldNotBeEmpty();

        var driver = GeneratorHarness.Run(new DraftGenerator(), input, out var output, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        driver.GetRunResult().Results.Single().GeneratedSources.ShouldHaveSingleItem();
        output.GetDiagnostics(TestContext.Current.CancellationToken).Select(d => d.ToString()).ShouldBe(expected, ignoreOrder: true);
    }

    // The [Obsolete] DiagnosticIds and [Experimental] ids generated code disables: an ASCII identifier, as #pragma reads it.
    [Theory]
    [InlineData("A", true)]
    [InlineData("Z", true)]
    [InlineData("a", true)]
    [InlineData("z", true)]
    [InlineData("_", true)]
    [InlineData("A0", true)]
    [InlineData("_9", true)]
    [InlineData("Ab_zZ09", true)]
    [InlineData("0", false)]
    [InlineData("9A", false)]
    [InlineData("@", false)]
    [InlineData("[", false)]
    [InlineData("`", false)]
    [InlineData("{", false)]
    [InlineData("A/", false)]
    [InlineData("A:", false)]
    [InlineData("A-1", false)]
    [InlineData("A 1", false)]
    [InlineData("\u00E9", false)]
    [InlineData("default", false)]
    [InlineData("hidden", false)]
    public void PragmaSafe_OnlyAsciiIdentifiers(string id, bool safe) => DraftModel.IsPragmaSafe(id).ShouldBe(safe);

    private static Task VerifyShape(Compilation compilation, string shape)
    {
        compilation.GetDiagnostics(TestContext.Current.CancellationToken).ShouldAllBe(d => d.Severity != DiagnosticSeverity.Error && d.Severity != DiagnosticSeverity.Warning);

        var driver = GeneratorHarness.Run(new DraftGenerator(), compilation, out var output, out var diagnostics);

        output.ShouldCompileClean();
        var sources = driver.GetRunResult().Results.Single().GeneratedSources
            .OrderBy(s => s.HintName, StringComparer.Ordinal)
            .Select(s => $"// {s.HintName}\n{s.SourceText}");
        var reported = diagnostics.Select(d => $"// {d.Severity} {d.Id} at {d.Location.GetLineSpan().Span}: {d.GetMessage(CultureInfo.InvariantCulture)}");
        return Verifier.Verify(string.Join("\n", reported.Concat(sources))).UseParameters(shape);
    }
}
