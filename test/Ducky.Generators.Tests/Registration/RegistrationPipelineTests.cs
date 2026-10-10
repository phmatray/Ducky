using Ducky.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Ducky.Generators.Tests.Registration;

// Caching_{Step}_Cached for each GEN-02 tracking name (GEN-06, INV-25): symbol-free equatable models, so an unrelated
// edit leaves every step Cached or Unchanged.
public sealed class RegistrationPipelineTests
{
    private const string Consumer = """
        #pragma warning disable CS1591 // justification: undocumented test source
        namespace App;
        public sealed record Cart(int Count);
        public sealed class CartSlice : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
        public sealed class CartEffects : Ducky.EffectGroup { }
        """;

    [Fact]
    public void Caching_RegistrationTypes_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new RegistrationGenerator(), GeneratorHarness.Compile(Consumer), "RegistrationTypes");

    [Fact]
    public void Caching_RegistrationGates_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new RegistrationGenerator(), GeneratorHarness.Compile(Consumer), "RegistrationGates");

    // The emit projection drops the models' Locations, so an edit to the slices' own file that shifts every span (new
    // RegistrationTypes outputs) leaves it Unchanged and the registration isn't regenerated.
    [Fact]
    public void Caching_RegistrationEmit_Cached()
    {
        var compilation = GeneratorHarness.Compile(Consumer);
        GeneratorHarness.ShouldCacheSteps(new RegistrationGenerator(), compilation, "RegistrationEmit");

        var driver = GeneratorHarness.Run(new RegistrationGenerator(), compilation, out _);
        var tree = compilation.SyntaxTrees[0];
        var edited = compilation.ReplaceSyntaxTree(tree, tree.WithChangedText(tree.GetText(TestContext.Current.CancellationToken).WithChanges(new TextChange(default, "// edited\n"))));
        var steps = driver.RunGenerators(edited, TestContext.Current.CancellationToken).GetRunResult().Results.Single().TrackedSteps;
        steps["RegistrationTypes"].SelectMany(s => s.Outputs).ShouldContain(o => o.Reason == IncrementalStepRunReason.Modified);
        steps["RegistrationEmit"].SelectMany(s => s.Outputs).ShouldAllBe(o => o.Reason == IncrementalStepRunReason.Unchanged || o.Reason == IncrementalStepRunReason.Cached);
    }

    // Against the real Ducky.Blazor, so the attributed slice raises no DUCKY022 and the harness sees a clean run.
    [Fact]
    public void Caching_RegistrationPersist_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new RegistrationGenerator(), Attributed(), "RegistrationPersist");

    [Fact]
    public void Caching_RegistrationPrerender_Cached() =>
        GeneratorHarness.ShouldCacheSteps(new RegistrationGenerator(), Attributed(), "RegistrationPrerender");

    private static Compilation Attributed() => DiagnosingHarness.WithBlazor(GeneratorHarness.Compile(Consumer + """
        [Ducky.Persist, Ducky.Prerender] public sealed class Saved : Ducky.Slice<Cart> { protected override Cart Initial => new(0); }
        """));
}
