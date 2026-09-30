using System.Reflection;

namespace Ducky.Draft.Tests;

// Stage 1 (SPEC §24): replaced by the first real Ducky.Draft.Tests story (M10-01), which deletes it and its manifest entry.
public sealed class SkeletonTests
{
    [Fact]
    public void Skeleton_DuckyDraftTests_Smoke()
    {
        var usage = typeof(DraftableAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        usage.ValidOn.ShouldBe(AttributeTargets.Class);
        usage.Inherited.ShouldBeFalse();
        new DraftableAttribute().ShouldBeAssignableTo<Attribute>();
    }
}
