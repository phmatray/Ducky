using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Draft.Generators.Tests;

// Stage 1 (SPEC §24): replaced by the first real Ducky.Draft.Generators.Tests story (M10-03), which deletes it and its manifest entry.
public sealed class SkeletonTests
{
    [Fact]
    public void Skeleton_DuckyDraftGeneratorsTests_Smoke()
    {
        var result = CSharpGeneratorDriver.Create(new DraftGenerator())
            .RunGenerators(CSharpCompilation.Create("Consumer"), TestContext.Current.CancellationToken)
            .GetRunResult()
            .Results.Single();

        result.Exception.ShouldBeNull();
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldBeEmpty();
    }
}
