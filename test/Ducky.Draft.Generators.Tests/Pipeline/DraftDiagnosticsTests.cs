using System.Collections.Immutable;
using System.Globalization;
using Ducky.Draft.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace Ducky.Draft.Generators.Tests.Pipeline;

// DUCKY101-104 and DUCKY108 (SPEC §13.3, §16.2): an unsupported [Draftable] shape is reported on the record's
// identifier and gets no output, never broken generated code, so the output still compiles.
public sealed class DraftDiagnosticsTests
{
    private const string Prefix = "#pragma warning disable CS1591 // justification: undocumented test sources\n";

    private static ImmutableArray<Diagnostic> Diagnose(string source, out Compilation input, out GeneratorDriver driver)
    {
        input = GeneratorHarness.Compile(Prefix + "namespace App;\n" + source);
        input.ShouldCompileClean();
        driver = GeneratorHarness.Run(new DraftGenerator(), input, out var output, out var diagnostics);
        output.ShouldCompileClean();
        return diagnostics;
    }

    public static TheoryData<string, string, string, string> InvalidShapes => new()
    {
        { "[Ducky.Draft.Draftable] public sealed record Todo(int Id);", "DUCKY101", "Todo", "'App.Todo' gets no draft: [Draftable] supports only a partial record class that is not file-local (not a record struct, a class, a non-partial record or a file-local type)" },
        { "[Ducky.Draft.Draftable] public sealed partial class Todo { }", "DUCKY101", "Todo", "'App.Todo' gets no draft: [Draftable] supports only a partial record class that is not file-local (not a record struct, a class, a non-partial record or a file-local type)" },
        // A file-local type can't take a partial part from another file.
        { "[Ducky.Draft.Draftable] file sealed partial record Todo(int Id);", "DUCKY101", "Todo", "'App.Todo' gets no draft: [Draftable] supports only a partial record class that is not file-local (not a record struct, a class, a non-partial record or a file-local type)" },
        { "file partial class Host { [Ducky.Draft.Draftable] public sealed partial record Item(int Id); }", "DUCKY101", "Item", "'App.Host.Item' gets no draft: [Draftable] supports only a partial record class that is not file-local (not a record struct, a class, a non-partial record or a file-local type)" },
        { "[Ducky.Draft.Draftable] public sealed partial record Box<T>(T Value);", "DUCKY103", "Box", "'App.Box<T>' gets no draft: it is generic or nested in a generic type, which Ducky.Draft 2.0 does not support" },
        { "public partial class Host<T> { [Ducky.Draft.Draftable] public sealed partial record Item(int Id); }", "DUCKY103", "Item", "'App.Host<T>.Item' gets no draft: it is generic or nested in a generic type, which Ducky.Draft 2.0 does not support" },
        { "public partial class Outer { public class Host { [Ducky.Draft.Draftable] public sealed partial record Item(int Id); } }", "DUCKY102", "Item", "'App.Outer.Host.Item' gets no draft: its containing type 'App.Outer.Host' is not partial" },
        { "public class Host { [Ducky.Draft.Draftable] public sealed partial record Item(int Id); }", "DUCKY102", "Item", "'App.Host.Item' gets no draft: its containing type 'App.Host' is not partial" },
        { "[Ducky.Draft.Draftable] public sealed partial record Todo(int Draft);", "DUCKY104", "Todo", "'App.Todo' gets no draft: it already declares a member named 'Draft', which the generator emits" },
        { "[Ducky.Draft.Draftable] public sealed partial record Todo { public void Produce() { } }", "DUCKY104", "Todo", "'App.Todo' gets no draft: it already declares a member named 'Produce', which the generator emits" },
        { "[Ducky.Draft.Draftable] public sealed partial record Todo { public int CreateDraft() => 0; }", "DUCKY104", "Todo", "'App.Todo' gets no draft: it already declares a member named 'CreateDraft', which the generator emits" },
        { "[Ducky.Draft.Draftable] public sealed partial record Todo { public sealed class Draft { } }", "DUCKY104", "Todo", "'App.Todo' gets no draft: it already declares a member named 'Draft', which the generator emits" },
        // The record's own name (CS0542 on the generated member) and inherited non-method members (hidden by the generated
        // member, and read through it) clash too; inherited methods and a nested type Draft don't (§13.3 `new`).
        { "[Ducky.Draft.Draftable] public sealed partial record Draft(string Subject);", "DUCKY104", "Draft", "'App.Draft' gets no draft: it already declares a member named 'Draft', which the generator emits" },
        { "[Ducky.Draft.Draftable] public sealed partial record Produce(int Kg);", "DUCKY104", "Produce", "'App.Produce' gets no draft: it already declares a member named 'Produce', which the generator emits" },
        { "[Ducky.Draft.Draftable] public sealed partial record CreateDraft(int X);", "DUCKY104", "CreateDraft", "'App.CreateDraft' gets no draft: it already declares a member named 'CreateDraft', which the generator emits" },
        { "public abstract record Base(int Draft); [Ducky.Draft.Draftable] public sealed partial record Todo(int Draft, bool F) : Base(Draft);", "DUCKY104", "Todo", "'App.Todo' gets no draft: it already declares a member named 'Draft', which the generator emits" },
        { "public abstract record Base { public int Produce { get; init; } } [Ducky.Draft.Draftable] public sealed partial record Todo(int Id) : Base;", "DUCKY104", "Todo", "'App.Todo' gets no draft: it already declares a member named 'Produce', which the generator emits" },
        { "public abstract record Base { public int CreateDraft; } [Ducky.Draft.Draftable] public sealed partial record Todo(int Id) : Base;", "DUCKY104", "Todo", "'App.Todo' gets no draft: it already declares a member named 'CreateDraft', which the generator emits" },
        { "public abstract record Base { public sealed class Produce { } } [Ducky.Draft.Draftable] public sealed partial record Todo(int Id) : Base;", "DUCKY104", "Todo", "'App.Todo' gets no draft: it already declares a member named 'Produce', which the generator emits" },
        { "[Ducky.Draft.Draftable, System.Obsolete(\"gone\", true)] public sealed partial record Todo(int Id);", "DUCKY108", "Todo", "'App.Todo' gets no draft: it is marked [Obsolete(error: true)], so generated code can't reference it (CS0619)" },
    };

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public void Ducky10x_InvalidShape_ReportedAndNothingEmitted(string source, string id, string typeName, string message)
    {
        var diagnostic = Diagnose(source, out var input, out var driver).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(id);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(message);
        var declared = input.GetSymbolsWithName(typeName, SymbolFilter.Type, TestContext.Current.CancellationToken).Single().Locations.Single();
        diagnostic.Location.IsInSource.ShouldBeTrue(); // so a #pragma in the user's source applies to it
        diagnostic.Location.SourceSpan.ShouldBe(declared.SourceSpan);
        diagnostic.Location.GetLineSpan().ShouldBe(declared.GetLineSpan());
        driver.GetRunResult().Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    // A record struct is DUCKY101 too, next to the compiler's own CS0592 for the Class-only attribute target.
    [Fact]
    public void Ducky101_RecordStruct_Reported()
    {
        var input = GeneratorHarness.Compile(Prefix + "namespace App;\n[Ducky.Draft.Draftable] public partial record struct Point(int X);");
        GeneratorHarness.Run(new DraftGenerator(), input, out _, out var diagnostics);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("DUCKY101");
    }

    // A parent whose member type is an invalid [Draftable] record reports only that record's DUCKY101 and compiles:
    // the member is a plain get/set (SPEC §13.3).
    [Fact]
    public void Ducky101_InvalidMemberType_OnlyItsDiagnostic()
    {
        var diagnostics = Diagnose("""
            [Ducky.Draft.Draftable] public sealed record Child(int Id);
            [Ducky.Draft.Draftable] public sealed partial record Parent(Child Child);
            """, out _, out var driver);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("DUCKY101");
        driver.GetRunResult().Results.Single().GeneratedSources.Select(s => s.HintName).ShouldBe(["App.Parent.Draft.g.cs"]);
    }

    // Inherited members that are no DUCKY104 (§13.3): an accessible inherited method named Produce or CreateDraft is
    // overloaded, and the generated CreateDraft() or Draft takes `new` over an accessible parameterless CreateDraft() or
    // nested type Draft; an inaccessible (private) inherited member is not hidden, so it neither clashes nor takes `new`,
    // and neither does a generic CreateDraft<T>(), Draft<T>() or Draft<T>, which the non-generic member doesn't hide.
    public static TheoryData<string> InheritedNoClash =>
    [
        "public abstract record Base { public void Produce() { } public void CreateDraft(int x) { } public sealed class Draft { } }",
        "public abstract record Base { public void CreateDraft() { } }",
        "public abstract record Base { private int Produce { get; init; } private int Draft { get; init; } private void CreateDraft() { } }",
        "public abstract record Base { private sealed class Draft { } }",
        "public abstract record Base { public void CreateDraft<T>() { } public class Draft<T> { } }",
        "public abstract record Base { public void Draft<T>() { } }",
    ];

    [Theory]
    [MemberData(nameof(InheritedNoClash))]
    public void Ducky104_InheritedMethodsAndDraftType_NotReported(string baseRecord)
    {
        var diagnostics = Diagnose(baseRecord + "\n[Ducky.Draft.Draftable] public sealed partial record Todo(int Id) : Base;", out _, out var driver);

        diagnostics.ShouldBeEmpty();
        driver.GetRunResult().Results.Single().GeneratedSources.ShouldHaveSingleItem();
    }
}
