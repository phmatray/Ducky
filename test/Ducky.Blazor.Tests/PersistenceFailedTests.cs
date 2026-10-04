namespace Ducky.Blazor.Tests;

// SPEC §11.5 (the public failure action); non-normative: keeps the record covered until the persistence middleware
// (M6-04) dispatches it.
public sealed class PersistenceFailedTests
{
    [Fact]
    public void PersistenceFailed_IsValueRecord()
    {
        var failure = new PersistenceFailed("cart", "QuotaExceededError", "The quota has been exceeded.");

        failure.ShouldBe(new PersistenceFailed("cart", "QuotaExceededError", "The quota has been exceeded."));
        (failure.SliceKey, failure.ErrorType, failure.Message).ShouldBe(("cart", "QuotaExceededError", "The quota has been exceeded."));
    }
}
