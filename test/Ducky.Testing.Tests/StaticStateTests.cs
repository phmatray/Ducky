using Ducky.TestSupport;

namespace Ducky.Testing.Tests;

public sealed class StaticStateTests
{
    [Fact]
    public void NoStaticMutableFields() =>
        StaticFieldAudit.Violations(typeof(DuckyAssertionException).Assembly).ShouldBeEmpty();
}
