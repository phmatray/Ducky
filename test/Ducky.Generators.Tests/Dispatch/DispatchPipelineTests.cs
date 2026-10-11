using Ducky.Generators.Tests.Infrastructure;

namespace Ducky.Generators.Tests.Dispatch;

// GEN-06 for GEN-01 (SPEC §16.1): hint names never crash the generator (CS8785) and every tracked step caches.
public sealed class DispatchPipelineTests
{
    private const string Consumer = """
        #pragma warning disable CS1591 // justification: undocumented test source
        namespace App;
        [Ducky.DuckyAction] public sealed record Increment(int By = 1);
        """;

    // Same-named records in two namespaces, case-only names, names equal once sanitized (non-ASCII), nesting two levels
    // deep and a keyword name: each gets its own valid hint name. AddSource compares hint names ignoring case.
    [Fact]
    public void HintNames_UniqueAndValid()
    {
        var compilation = GeneratorHarness.Compile("""
            #pragma warning disable CS1591, CS8981 // justification: undocumented test source; lower-case names are the shapes under test
            namespace A { [Ducky.DuckyAction] public sealed record Todo; }
            namespace B { [Ducky.DuckyAction] public sealed record Todo(int Id); }
            namespace C
            {
                [Ducky.DuckyAction] public sealed record Inc;
                [Ducky.DuckyAction] public sealed record inc;
                [Ducky.DuckyAction] public sealed record INC;
                [Ducky.DuckyAction] public sealed record inc_2;
                [Ducky.DuckyAction] public sealed record Café;
                [Ducky.DuckyAction] public sealed record Caf_;
                public static class Outer { public static class Inner { [Ducky.DuckyAction] public sealed record Leaf; } }
                [Ducky.DuckyAction] public sealed record @class;
            }
            """);

        var driver = GeneratorHarness.Run(new DispatchExtensionsGenerator(), compilation, out var output);

        output.ShouldCompileClean();
        driver.GetRunResult().Results.Single().GeneratedSources.Select(s => s.HintName).ShouldBe([
            "A.Todo.DispatchExtensions.g.cs",
            "B.Todo.DispatchExtensions.g.cs",
            "C.Inc.DispatchExtensions.g.cs",
            "C.inc_2.DispatchExtensions.g.cs",
            "C.INC_3.DispatchExtensions.g.cs",
            "C.inc_2_2.DispatchExtensions.g.cs",
            "C.Caf_.DispatchExtensions.g.cs",
            "C.Caf__2.DispatchExtensions.g.cs",
            "C.Outer.Inner.Leaf.DispatchExtensions.g.cs",
            "C._class.DispatchExtensions.g.cs",
        ]);
    }

    [Fact]
    public void Caching_DispatchActions_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new DispatchExtensionsGenerator(), GeneratorHarness.Compile(Consumer), "DispatchActions");

    [Fact]
    public void Caching_DispatchReservedNames_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new DispatchExtensionsGenerator(), GeneratorHarness.Compile(Consumer), "DispatchReservedNames");

    [Fact]
    public void Caching_DispatchOutput_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new DispatchExtensionsGenerator(), GeneratorHarness.Compile(Consumer), "DispatchOutput");
}
