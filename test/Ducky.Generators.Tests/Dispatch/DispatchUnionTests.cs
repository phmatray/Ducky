using System.Globalization;
using Ducky.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators.Tests.Dispatch;

// #314: [DuckyAction] on a C# union (ADR-0050) generates the helpers of each case, named after the case, through the same
// case reader as union slices; MethodName set on the union is ignored with DUCKY028.
public sealed class DispatchUnionTests
{
    private const string Cases = """
        #pragma warning disable CS1591 // justification: undocumented test sources
        namespace App;
        public sealed record AddItem(string Name);
        public sealed record RemoveItem(string Name);
        public sealed record Clear;
        public sealed record Other;
        public union Inner(RemoveItem, Other);

        """;

    // A good test here fails if a case gets no helper: four cases, two of them through a nested union, four files.
    [Fact]
    public void Union_GeneratesOneHelperPerCase()
    {
        GeneratorHarness.Run(new DispatchExtensionsGenerator(), GeneratorHarness.Compile(Cases + """
            [Ducky.DuckyAction] public union CartAction(AddItem, Inner, Clear);
            """), out var output);

        output.ShouldCompileClean();
        var generated = output.SyntaxTrees.Where(t => t.FilePath.EndsWith(".DispatchExtensions.g.cs", StringComparison.Ordinal)).Select(t => t.ToString()).ToList();
        generated.Count.ShouldBe(4);
        generated.ShouldContain(g => g.Contains("public static void AddItem(this global::Ducky.IDispatcher __dispatcher, string Name) =>", StringComparison.Ordinal)
            && g.Contains("__dispatcher.Dispatch(new global::App.AddItem(Name));", StringComparison.Ordinal));
        generated.ShouldContain(g => g.Contains("public static void RemoveItem(this global::Ducky.IDispatcher __dispatcher, string Name) =>", StringComparison.Ordinal));
        generated.ShouldContain(g => g.Contains("public static void Other(this global::Ducky.IDispatcher __dispatcher) =>", StringComparison.Ordinal));
        generated.ShouldContain(g => g.Contains("public static void Clear(this global::Ducky.IDispatcher __dispatcher) =>", StringComparison.Ordinal));
        generated.ShouldNotContain(g => g.Contains("CartAction(", StringComparison.Ordinal));
    }

    [Fact]
    public void Union_WithMethodName_IsDucky028_AndStillNamesHelpersAfterCases()
    {
        var input = GeneratorHarness.Compile(Cases + """
            [Ducky.DuckyAction(MethodName = "Cart")] public union CartAction(AddItem, Clear);
            """);
        DiagnosingHarness.Run(new DispatchExtensionsGenerator(), input, out var output, out var diagnostics);

        output.ShouldCompileClean();
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("DUCKY028");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe("MethodName on the union 'App.CartAction' is ignored: each case's dispatch helpers are named after the case");
        var declared = output.GetSymbolsWithName("CartAction", SymbolFilter.Type, TestContext.Current.CancellationToken).Single().Locations[0];
        diagnostic.Location.SourceSpan.ShouldBe(declared.SourceSpan);
        output.SyntaxTrees.ShouldContain(t => t.ToString().Contains("static void AddItem(", StringComparison.Ordinal));
        output.SyntaxTrees.ShouldNotContain(t => t.ToString().Contains("static void Cart(", StringComparison.Ordinal));
    }

    // A case that can't get a helper (an abstract record) is skipped, without failing the union; the others get theirs.
    [Fact]
    public void Union_CaseWithoutUsableConstructor_IsSkipped()
    {
        var input = GeneratorHarness.Compile(Cases + """
            public abstract record Shape;
            [Ducky.DuckyAction] public union CartAction(AddItem, Shape);
            """);
        DiagnosingHarness.Run(new DispatchExtensionsGenerator(), input, out var output, out var diagnostics);

        output.ShouldCompileClean();
        diagnostics.ShouldBeEmpty();
        output.SyntaxTrees.Count(t => t.FilePath.EndsWith(".DispatchExtensions.g.cs", StringComparison.Ordinal)).ShouldBe(1);
    }

    // A nested union that carries [DuckyAction] too: its cases are generated once, with no DUCKY020.
    [Fact]
    public void Union_NestedAttributedUnion_GeneratesEachCaseOnce()
    {
        var input = GeneratorHarness.Compile(Cases + """
            [Ducky.DuckyAction] public union CartAction(AddItem, Clear);
            [Ducky.DuckyAction] public union AppAction(CartAction, Other);
            """);
        DiagnosingHarness.Run(new DispatchExtensionsGenerator(), input, out var output, out var diagnostics);

        output.ShouldCompileClean();
        diagnostics.ShouldBeEmpty();
        output.SyntaxTrees.Count(t => t.FilePath.EndsWith(".DispatchExtensions.g.cs", StringComparison.Ordinal)).ShouldBe(3);
    }

    // A case carrying its own [DuckyAction] keeps its own helpers (and MethodName); a case shared by two unions gets one set;
    // a case declared outside the project (string) gets none. Each class is generated once, so nothing is DUCKY020.
    [Fact]
    public void Union_SharedOwnAndForeignCases_EachCaseGeneratedOnce()
    {
        var input = GeneratorHarness.Compile(Cases + """
            [Ducky.DuckyAction(MethodName = "Wipe")] public sealed record Reset;
            [Ducky.DuckyAction] public union CartAction(AddItem, Reset, string);
            [Ducky.DuckyAction] public union OtherAction(AddItem, Clear);
            """);
        DiagnosingHarness.Run(new DispatchExtensionsGenerator(), input, out var output, out var diagnostics);

        output.ShouldCompileClean();
        diagnostics.ShouldBeEmpty();
        var generated = output.SyntaxTrees.Where(t => t.FilePath.EndsWith(".DispatchExtensions.g.cs", StringComparison.Ordinal)).Select(t => t.ToString()).ToList();
        generated.Count.ShouldBe(3);
        generated.Count(g => g.Contains("static void AddItem(", StringComparison.Ordinal)).ShouldBe(1);
        generated.ShouldContain(g => g.Contains("static void Wipe(", StringComparison.Ordinal));
        generated.ShouldContain(g => g.Contains("static void Clear(", StringComparison.Ordinal));
        generated.ShouldNotContain(g => g.Contains("String", StringComparison.Ordinal));
    }

    // A generic union is reported as generic, as a generic record is.
    [Fact]
    public void Union_Generic_IsDucky020()
    {
        var input = GeneratorHarness.Compile(Cases + """
            [Ducky.DuckyAction] public union Box<T>(AddItem, Clear);
            """);
        DiagnosingHarness.Run(new DispatchExtensionsGenerator(), input, out _, out var diagnostics);

        diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldBe("'App.Box<T>' gets no dispatch helper: it is generic or nested in a generic type");
    }

    [Fact]
    public void Caching_DispatchTargets_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new DispatchExtensionsGenerator(), GeneratorHarness.Compile(Cases + """
            [Ducky.DuckyAction] public union CartAction(AddItem, Clear);
            """), "DispatchTargets", "DispatchActions");
}
