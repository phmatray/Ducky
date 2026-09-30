using Ducky.TestSupport;

namespace Ducky.Generators.Tests;

public sealed class StaticStateTests
{
    [Fact]
    public void NoStaticMutableFields() =>
        StaticFieldAudit.Violations(typeof(DispatchExtensionsGenerator).Assembly).ShouldBeEmpty();
}
