namespace Ducky.Tests;

// Stage 1 (SPEC §24): replaced by the first real Ducky.Tests story (M1-01), which deletes it and its manifest entry.
public sealed class SkeletonTests
{
    [Fact]
    public void Skeleton_DuckyTests_Smoke() =>
        new ActionTypeAttribute("cart/Add").Name.ShouldBe("cart/Add");
}
