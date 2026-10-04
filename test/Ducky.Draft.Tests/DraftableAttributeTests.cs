using System.Reflection;

namespace Ducky.Draft.Tests;

// SPEC §13.2: [AttributeUsage(AttributeTargets.Class, Inherited = false)]. Non-normative test of story M10-01.
public sealed class DraftableAttributeTests
{
    [Fact]
    public void DraftableAttribute_Usage_MatchesSpec()
    {
        var usage = typeof(DraftableAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        usage.ValidOn.ShouldBe(AttributeTargets.Class);
        usage.Inherited.ShouldBeFalse();
        usage.AllowMultiple.ShouldBeFalse();
    }
}
