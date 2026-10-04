using System.Reflection;

namespace Ducky.Tests.Contract;

// SPEC §5.8 (non-normative): the attributes the generator reads, with their usages and defaults. The runtime never reflects
// over [DuckyAction], [Persist] or [Prerender]; GEN-02 turns them into builder calls.
public sealed class AttributeTests
{
    [Theory]
    [InlineData(typeof(DuckyActionAttribute), AttributeTargets.Class | AttributeTargets.Struct)]
    [InlineData(typeof(PersistAttribute), AttributeTargets.Class)]
    [InlineData(typeof(PrerenderAttribute), AttributeTargets.Class)]
    public void Attribute_Usage_MatchesSpec(Type attribute, AttributeTargets targets)
    {
        var usage = attribute.GetCustomAttribute<AttributeUsageAttribute>().ShouldNotBeNull();

        usage.ValidOn.ShouldBe(targets);
        usage.Inherited.ShouldBeFalse();
        usage.AllowMultiple.ShouldBeFalse();
        attribute.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void Attribute_Properties_DefaultsAndInit()
    {
        new DuckyActionAttribute().MethodName.ShouldBeNull();
        new DuckyActionAttribute { MethodName = "Go" }.MethodName.ShouldBe("Go");
        var defaults = new PersistAttribute();
        (defaults.Version, defaults.Storage, defaults.SyncAcrossTabs).ShouldBe((1, PersistStorage.Local, false));
        var set = new PersistAttribute { Version = 2, Storage = PersistStorage.Server, SyncAcrossTabs = true };
        (set.Version, set.Storage, set.SyncAcrossTabs).ShouldBe((2, PersistStorage.Server, true));
        new PrerenderAttribute().ShouldBeAssignableTo<Attribute>();
        Enum.GetNames<PersistStorage>().ShouldBe(["Local", "Session", "Server"]);
    }
}
