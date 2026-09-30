namespace Ducky.Docs.Tests;

// Stage 1 (SPEC §24): replaced by the first docs story (M15-01), which deletes it and its manifest entry.
public sealed class SkeletonTests
{
    [Fact]
    public void Skeleton_DuckyDocsTests_Smoke()
    {
        #region ActionTypeAttribute
        var attribute = new ActionTypeAttribute("cart/Add");
        #endregion

        attribute.Name.ShouldBe("cart/Add");
    }
}
