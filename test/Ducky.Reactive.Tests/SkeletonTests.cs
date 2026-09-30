using R3;

namespace Ducky.Reactive.Tests;

// Stage 1 (SPEC §24): replaced by the first real Ducky.Reactive.Tests story (M11-01), which deletes it and its manifest entry.
public sealed class SkeletonTests
{
    [Fact]
    public void Skeleton_DuckyReactiveTests_Smoke()
    {
        using var actions = new Subject<object>();
        var seen = new List<Increment>();
        using var subscription = actions.OfActionType<Increment>().Subscribe(seen.Add);

        actions.OnNext(new Increment(1));
        actions.OnNext(new DerivedIncrement(2)); // exact type only (ADR-0008)
        actions.OnNext("unrelated");

        seen.ShouldBe([new Increment(1)]);
    }

    private record Increment(int By);

    private sealed record DerivedIncrement(int By) : Increment(By);
}
