namespace Ducky.Testing.Tests;

// Stage 1 (SPEC §24): replaced by the first real Ducky.Testing.Tests story (M9-04), which deletes it and its manifest entry.
public sealed class SkeletonTests
{
    [Fact]
    public void Skeleton_DuckyTestingTests_Smoke() =>
        Should.Throw<DuckyAssertionException>(() => throw new DuckyAssertionException())
            .ShouldBeAssignableTo<Exception>();
}
