namespace Ducky.Blazor.Tests;

// Stage 1 (SPEC §24): replaced by the first real Ducky.Blazor.Tests story (M5-02), which deletes it and its manifest entry.
public sealed class SkeletonTests
{
    [Fact]
    public void Skeleton_DuckyBlazorTests_Smoke()
    {
        var failure = new PersistenceFailed("cart", "QuotaExceededError", "The quota has been exceeded.");

        failure.ShouldBe(new PersistenceFailed("cart", "QuotaExceededError", "The quota has been exceeded."));
        failure.SliceKey.ShouldBe("cart");
        failure.ErrorType.ShouldBe("QuotaExceededError");
        failure.Message.ShouldBe("The quota has been exceeded.");
    }
}
