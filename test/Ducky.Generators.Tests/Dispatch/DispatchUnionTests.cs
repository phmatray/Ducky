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

    // A good test here fails if a case gets no helper: three cases, one of them through a nested union, three files.
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

    // A case's own error is reported at the union, naming the case: a case with no usable constructor gets no helper.
    [Fact]
    public void Union_CaseWithoutUsableConstructor_IsDucky020AtTheUnion()
    {
        var input = GeneratorHarness.Compile(Cases + """
            public abstract record Shape;
            [Ducky.DuckyAction] public union CartAction(AddItem, Shape);
            """);
        DiagnosingHarness.Run(new DispatchExtensionsGenerator(), input, out var output, out var diagnostics);

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("DUCKY020");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldStartWith("'App.Shape' gets no dispatch helper");
        var declared = output.GetSymbolsWithName("CartAction", SymbolFilter.Type, TestContext.Current.CancellationToken).Single().Locations[0];
        diagnostic.Location.SourceSpan.ShouldBe(declared.SourceSpan);
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
