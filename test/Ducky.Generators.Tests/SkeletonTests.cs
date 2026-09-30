using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Generators.Tests;

// Stage 1 (SPEC §24): replaced by the first real Ducky.Generators.Tests story (M13-01), which deletes it and its manifest entry.
public sealed class SkeletonTests
{
    [Fact]
    public void Skeleton_DuckyGeneratorsTests_Smoke()
    {
        var result = CSharpGeneratorDriver.Create(new DispatchExtensionsGenerator())
            .RunGenerators(CSharpCompilation.Create("Consumer"), TestContext.Current.CancellationToken)
            .GetRunResult()
            .Results.Single();

        result.Exception.ShouldBeNull();
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldBeEmpty();
    }
}
