using System.Collections.Immutable;
using System.Globalization;
using Ducky.Draft.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;

namespace Ducky.Draft.Generators.Tests.Mapping;

// Symbol-based member mapping across assemblies (SPEC §13.3): a member type from another assembly maps to C.Draft only
// when C has a nested public Draft implementing IDraft<C> with a public Draft(C) constructor; a [Draftable] type without
// one is a plain get/set member with DUCKY107. Mutable collections are plain get/set members with DUCKY105.
public sealed class DraftMappingTests
{
    private const string Undocumented = "#pragma warning disable CS1591 // justification: undocumented test sources\n";

    private const string App = """
        using System.Collections.Immutable;
        namespace App;
        [Ducky.Draft.Draftable]
        public sealed partial record Holder(Lib.Item Item, Lib.Item? Maybe, ImmutableList<Lib.Item> Items, ImmutableDictionary<string, Lib.Item> ById, Lib.Plain Plain);
        """;

    // Lib compiled with the generator: its records map to Lib.Item.Draft, and a record deriving from its draftable Base
    // takes `new` over the inherited Draft and CreateDraft.
    [Fact]
    public Task Generated_CompilesClean_DraftShapeCrossAssembly()
    {
        var source = GeneratorHarness.Compile(Undocumented + """
            namespace Lib;
            [Ducky.Draft.Draftable] public sealed partial record Item(int Id);
            [Ducky.Draft.Draftable] public partial record Base(int X);
            public sealed record Plain(int Y);
            """).WithAssemblyName("Lib");
        GeneratorHarness.Run(new DraftGenerator(), source, out var lib);
        lib.ShouldCompileClean();

        return VerifyApp(App + "\n[Ducky.Draft.Draftable] public sealed partial record Derived(int X, int Y) : Lib.Base(X);", lib, []);
    }

    // [Draftable] in metadata is not enough: Lib built without the generator, or with a nested Draft that is not public,
    // doesn't implement IDraft<Item>, or lacks a public Draft(Item) constructor, or whose Draft or Draft(Item) generated
    // code can't name cleanly (error-obsolete: CS0619; an [Obsolete] DiagnosticId or an [Experimental] id: not CS0618).
    public static TheoryData<string> IncompatibleDrafts =>
    [
        "",
        "internal sealed class Draft(Item original) : Ducky.Draft.IDraft<Item> { public bool IsDirty => false; public Item Build() => original; void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft(Item original) : Ducky.Draft.IDraft<object> { public bool IsDirty => false; public object Build() => original; void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft : Ducky.Draft.IDraft<Item> { public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft : Ducky.Draft.IDraft<Item> { internal Draft(Item original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft : Ducky.Draft.IDraft<Item> { public Draft(Item original, int x) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft : Ducky.Draft.IDraft<Item> { public Draft(Plain original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft : Ducky.Draft.IDraft<Item> { public static Draft Create(Item original) => new(); public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft<T> : Ducky.Draft.IDraft<Item> { public Draft(Item original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "public struct Draft : Ducky.Draft.IDraft<Item> { public Draft(Item original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "public abstract class Draft : Ducky.Draft.IDraft<Item> { public Draft(Item original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft : Ducky.Draft.IDraft<Item> { public Draft(ref Item original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "[System.Obsolete(\"gone\", true)] public sealed class Draft(Item original) : Ducky.Draft.IDraft<Item> { public bool IsDirty => false; public Item Build() => original; void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft : Ducky.Draft.IDraft<Item> { [System.Obsolete(\"gone\", true)] public Draft(Item original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "[System.Obsolete(\"old\", DiagnosticId = \"LIB001\")] public sealed class Draft(Item original) : Ducky.Draft.IDraft<Item> { public bool IsDirty => false; public Item Build() => original; void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft : Ducky.Draft.IDraft<Item> { [System.Obsolete(\"old\", DiagnosticId = \"LIB001\")] public Draft(Item original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
        "[System.Diagnostics.CodeAnalysis.Experimental(\"LIB002\")] public sealed class Draft(Item original) : Ducky.Draft.IDraft<Item> { public bool IsDirty => false; public Item Build() => original; void Ducky.Draft.IRevocable.Revoke() { } }",
        "public sealed class Draft : Ducky.Draft.IDraft<Item> { [System.Diagnostics.CodeAnalysis.Experimental(\"LIB002\")] public Draft(Item original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
    ];

    [Theory]
    [MemberData(nameof(IncompatibleDrafts))]
    public void Ducky107_IncompatibleDraftInMetadata_PlainMemberReported(string draft)
    {
        var lib = Lib(draft);

        var diagnostics = Generate(App, lib, out var output);

        output.ShouldCompileClean();
        diagnostics.Select(Describe).ShouldBe([
            "Info DUCKY107 at (4,45)-(4,49): 'App.Holder.Item' gets no draft of 'Lib.Item': 'Lib.Item' is [Draftable] in another assembly but has no nested public Draft implementing IDraft<Lib.Item> with a public Draft(Lib.Item) constructor; it is edited by assignment",
            "Info DUCKY107 at (4,61)-(4,66): 'App.Holder.Maybe' gets no draft of 'Lib.Item': 'Lib.Item' is [Draftable] in another assembly but has no nested public Draft implementing IDraft<Lib.Item> with a public Draft(Lib.Item) constructor; it is edited by assignment",
            "Info DUCKY107 at (4,92)-(4,97): 'App.Holder.Items' gets no draft of 'Lib.Item': 'Lib.Item' is [Draftable] in another assembly but has no nested public Draft implementing IDraft<Lib.Item> with a public Draft(Lib.Item) constructor; it is edited by assignment",
            "Info DUCKY107 at (4,137)-(4,141): 'App.Holder.ById' gets no draft of 'Lib.Item': 'Lib.Item' is [Draftable] in another assembly but has no nested public Draft implementing IDraft<Lib.Item> with a public Draft(Lib.Item) constructor; it is edited by assignment",
        ]);
        var holder = output.GetTypeByMetadataName("App.Holder+Draft")!;
        holder.GetMembers("Item").OfType<IPropertySymbol>().Single().Type.ToDisplayString().ShouldBe("Lib.Item");
        holder.GetMembers("Items").OfType<IPropertySymbol>().Single().Type.ToDisplayString().ShouldBe("Ducky.Draft.ListDraft<Lib.Item>");
    }

    // A hand-written compatible Draft in metadata (other constructors allowed; an [Obsolete] warning without a DiagnosticId
    // is CS0618, which generated code disables) is used like a generated one, and a metadata type without [Draftable] is
    // plain and not reported.
    public static TheoryData<string> CompatibleDrafts =>
    [
        "public sealed class Draft(Item original) : Ducky.Draft.IDraft<Item> { public Draft() : this(new Item(0)) { } public bool IsDirty => false; public Item Build() => original; void Ducky.Draft.IRevocable.Revoke() { } }",
        "[System.Obsolete(\"old\", DiagnosticId = \"\")] public sealed class Draft : Ducky.Draft.IDraft<Item> { [System.Obsolete(\"old\")] public Draft(Item original) { } public bool IsDirty => false; public Item Build() => new(0); void Ducky.Draft.IRevocable.Revoke() { } }",
    ];

    [Theory]
    [MemberData(nameof(CompatibleDrafts))]
    public void Ducky107_CompatibleDraftInMetadata_MappedNotReported(string draft)
    {
        var lib = Lib(draft);

        var diagnostics = Generate(App, lib, out var output);

        output.ShouldCompileClean();
        diagnostics.ShouldBeEmpty();
        var holder = output.GetTypeByMetadataName("App.Holder+Draft")!;
        holder.GetMembers("Item").OfType<IPropertySymbol>().Single().Type.ToDisplayString().ShouldBe("Lib.Item.Draft");
        holder.GetMembers("Plain").OfType<IPropertySymbol>().Single().Type.ToDisplayString().ShouldBe("Lib.Plain");
    }

    // A member inherited from a base in another assembly has no source location here: DUCKY107 goes on the record.
    [Fact]
    public void Ducky107_InheritedFromMetadata_ReportedOnRecord()
    {
        var lib = GeneratorHarness.Compile(Undocumented + """
            namespace Lib;
            [Ducky.Draft.Draftable] public sealed record Item(int Id);
            public abstract record Base(Item Inherited);
            """).WithAssemblyName("Lib");
        lib.ShouldCompileClean();

        var diagnostics = Generate("namespace App;\n[Ducky.Draft.Draftable] public sealed partial record Heir(Lib.Item Inherited) : Lib.Base(Inherited);", lib, out var output);

        output.ShouldCompileClean();
        diagnostics.Select(Describe).ShouldBe([
            "Info DUCKY107 at (2,53)-(2,57): 'Lib.Base.Inherited' gets no draft of 'Lib.Item': 'Lib.Item' is [Draftable] in another assembly but has no nested public Draft implementing IDraft<Lib.Item> with a public Draft(Lib.Item) constructor; it is edited by assignment",
        ]);
    }

    // The nullable-annotated elements are never element-drafted, so their type is never checked (no DUCKY107).
    [Fact]
    public void Ducky107_NullableElement_NotChecked()
    {
        var diagnostics = Generate(
            "using System.Collections.Immutable;\nnamespace App;\n[Ducky.Draft.Draftable] public sealed partial record Holder(ImmutableList<Lib.Item?> Items);",
            Lib(""),
            out var output);

        output.ShouldCompileClean();
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Ducky105_MutableCollections_ReportedOnMembers()
    {
        var diagnostics = Generate("""
            using System.Collections.Generic;
            namespace App;
            [Ducky.Draft.Draftable] public sealed partial record Bag(List<int> Items, int[] Numbers, Dictionary<string, int> Map, HashSet<int> Set);
            """, null, out var output);

        output.ShouldCompileClean();
        diagnostics.Select(Describe).ShouldBe([
            "Warning DUCKY105 at (3,67)-(3,72): 'App.Bag.Items' is a plain member of the draft: its type 'System.Collections.Generic.List<int>' is a mutable collection, so structural sharing and immutability can't be guaranteed",
            "Warning DUCKY105 at (3,80)-(3,87): 'App.Bag.Numbers' is a plain member of the draft: its type 'int[]' is a mutable collection, so structural sharing and immutability can't be guaranteed",
            "Warning DUCKY105 at (3,113)-(3,116): 'App.Bag.Map' is a plain member of the draft: its type 'System.Collections.Generic.Dictionary<string, int>' is a mutable collection, so structural sharing and immutability can't be guaranteed",
        ]);
    }

    // Reported at the member's declaration in the user's tree, so #pragma warning disable suppresses it.
    [Fact]
    public void Ducky105_Pragma_Suppressed()
    {
        var diagnostics = Generate("""
            namespace App;
            #pragma warning disable DUCKY105 // justification: a mutable buffer on purpose
            [Ducky.Draft.Draftable] public sealed partial record Bag(int[] Numbers);
            #pragma warning restore DUCKY105
            """, null, out _);

        diagnostics.ShouldHaveSingleItem().IsSuppressed.ShouldBeTrue();
    }

    // An inherited member is reported once: by the draftable base in this compilation that declares it, else on each
    // deriving record's name (never at a declaration of another, possibly non-draftable, type).
    [Fact]
    public void Ducky105_InheritedMember_ReportedOnce()
    {
        var diagnostics = Generate("""
            using System.Collections.Generic;
            namespace App;
            [Ducky.Draft.Draftable] public partial record Base(List<int> Items);
            [Ducky.Draft.Draftable] public sealed partial record Derived(List<int> Items, int Y) : Base(Items);
            [Ducky.Draft.Draftable] public sealed partial record Other(List<int> Items, int Z) : Base(Items);
            public record Plain(int[] Values);
            [Ducky.Draft.Draftable] public sealed partial record Heir(int[] Values) : Plain(Values);
            """, null, out var output);

        output.ShouldCompileClean();
        diagnostics.Select(d => $"{d.Id} at {d.Location.GetLineSpan().Span}: {d.GetMessage(CultureInfo.InvariantCulture).Split(':')[0]}").ShouldBe([
            "DUCKY105 at (3,61)-(3,66): 'App.Base.Items' is a plain member of the draft",
            "DUCKY105 at (7,53)-(7,57): 'App.Plain.Values' is a plain member of the draft",
        ]);
    }

    // A record that gets no draft reports only its own diagnostic, never its members'.
    [Fact]
    public void Ducky105_InvalidRecord_OnlyItsDiagnostic()
    {
        var diagnostics = Generate("namespace App;\n[Ducky.Draft.Draftable] public sealed record Bag(System.Collections.Generic.List<int> Items);", null, out _);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("DUCKY101");
    }

    // A [Draftable] base that fails the shape predicate gets no Draft or CreateDraft, so the derived record's take no `new`
    // (CS0109 would fail the zero-diagnostics check).
    [Fact]
    public void Mapping_InvalidDraftableBase_DerivedTakesNoNew()
    {
        var diagnostics = Generate("""
            namespace App;
            [Ducky.Draft.Draftable] public record Base(int X);
            [Ducky.Draft.Draftable] public sealed partial record Derived(int X, int Y) : Base(X);
            """, null, out var output);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("DUCKY101");
        output.ShouldCompileClean();
        output.SyntaxTrees.Last().ToString().ShouldContain("public sealed class Draft");
    }

    // The parent's model carries whether a same-compilation member type passes the shape predicate: making the member
    // type invalid re-keys the parent, whose member becomes a plain get/set.
    [Fact]
    public void Mapping_MemberTypeInvalidated_ParentReEmitted()
    {
        const string Parent = "namespace App;\n[Ducky.Draft.Draftable] public sealed partial record Parent(Child Child);";
        var compilation = GeneratorHarness.Compile(Undocumented + Parent, Undocumented + "namespace App;\n[Ducky.Draft.Draftable] public sealed partial record Child(int Id);");
        var driver = GeneratorHarness.Run(new DraftGenerator(), compilation, out _, out _);
        driver.GetRunResult().Results.Single().GeneratedSources.Single(s => s.HintName == "App.Parent.Draft.g.cs").SourceText.ToString()
            .ShouldContain("global::App.Child.Draft Child");
        var child = compilation.SyntaxTrees.Last();
        var edited = compilation.ReplaceSyntaxTree(child, CSharpSyntaxTree.ParseText(Undocumented + "namespace App;\n[Ducky.Draft.Draftable] public sealed record Child(int Id);", GeneratorHarness.ParseOptions, child.FilePath, cancellationToken: TestContext.Current.CancellationToken));

        var parent = driver.RunGenerators(edited, TestContext.Current.CancellationToken).GetRunResult().Results.Single()
            .GeneratedSources.Single(s => s.HintName == "App.Parent.Draft.g.cs").SourceText.ToString();

        parent.ShouldContain("public global::App.Child Child");
        parent.ShouldNotContain("Child.Draft");
    }

    private static CSharpCompilation Lib(string draft)
    {
        var lib = GeneratorHarness.Compile(Undocumented + $$"""
            namespace Lib;
            [Ducky.Draft.Draftable] public sealed record Item(int Id) { {{draft}} }
            public sealed record Plain(int Y);
            """).WithAssemblyName("Lib");
        lib.ShouldCompileClean();
        return lib;
    }

    private static ImmutableArray<Diagnostic> Generate(string app, Compilation? lib, out Compilation output)
    {
        var input = GeneratorHarness.Compile(Undocumented + app);
        if (lib is not null)
        {
            input = input.AddReferences(lib.ToMetadataReference());
        }

        input.ShouldCompileClean();
        GeneratorHarness.Run(new DraftGenerator(), input, out output, out var diagnostics);
        return diagnostics;
    }

    private static string Describe(Diagnostic d) =>
        $"{d.Severity} {d.Id} at {d.Location.GetLineSpan().Span}: {d.GetMessage(CultureInfo.InvariantCulture)}";

    private static Task VerifyApp(string app, Compilation lib, string[] expected)
    {
        var input = GeneratorHarness.Compile(Undocumented + app).AddReferences(lib.ToMetadataReference());
        input.ShouldCompileClean();
        var driver = GeneratorHarness.Run(new DraftGenerator(), input, out var output, out var diagnostics);
        output.ShouldCompileClean();
        diagnostics.Select(Describe).ShouldBe(expected);
        var sources = driver.GetRunResult().Results.Single().GeneratedSources
            .OrderBy(s => s.HintName, StringComparer.Ordinal)
            .Select(s => $"// {s.HintName}\n{s.SourceText}");
        return Verifier.Verify(string.Join("\n", sources));
    }
}
