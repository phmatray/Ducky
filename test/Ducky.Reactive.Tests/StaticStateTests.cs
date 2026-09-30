using Ducky.TestSupport;

namespace Ducky.Reactive.Tests;

public sealed class StaticStateTests
{
    [Fact]
    public void NoStaticMutableFields() =>
        StaticFieldAudit.Violations(typeof(DuckyReactiveExtensions).Assembly).ShouldBeEmpty();
}
