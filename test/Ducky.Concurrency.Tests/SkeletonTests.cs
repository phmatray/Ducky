namespace Ducky.Concurrency.Tests;

// Stage 1 (SPEC §24): replaced by the first real concurrency story (M1-13), which deletes it and its manifest entry.
public sealed class SkeletonTests
{
    public static TheoryData<int> Repeat() =>
        [.. Enumerable.Range(1, int.TryParse(Environment.GetEnvironmentVariable("DUCKY_REPEAT"), out var n) ? n : 50)];

    [Theory]
    [MemberData(nameof(Repeat))]
    public async Task Skeleton_DuckyConcurrencyTests_Smoke(int repeat)
    {
        _ = repeat;
        var attribute = new ActionTypeAttribute("cart/Add");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var barrier = new Barrier(2);

        var reads = Enumerable.Range(0, 2).Select(_ => Task.Run(
            () =>
            {
                barrier.SignalAndWait(cancellationToken);
                return attribute.Name;
            },
            cancellationToken));
        var names = await Task.WhenAll(reads).WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, cancellationToken);

        names.ShouldBe(["cart/Add", "cart/Add"]);
    }
}
