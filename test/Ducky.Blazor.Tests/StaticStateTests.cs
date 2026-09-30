using Ducky.TestSupport;

namespace Ducky.Blazor.Tests;

public sealed class StaticStateTests
{
    [Fact]
    public void NoStaticMutableFields() =>
        StaticFieldAudit.Violations(typeof(PersistenceFailed).Assembly).ShouldBeEmpty();
}
